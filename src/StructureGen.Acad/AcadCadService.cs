using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using StructureGen.Acad.Internal;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Geometry;
using StructureGen.Services;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace StructureGen.Acad
{
    /// <summary>
    /// The AutoCAD side of <see cref="ICadService"/>: everything the palette asks for, expressed in
    /// terms of documents, transactions and entities.
    /// </summary>
    /// <remarks>
    /// This class is the only one in the plugin that both knows about AutoCAD and is reachable from
    /// the user interface. Keeping the boundary here is what allows the palette, the schedule, the
    /// weights and the exporter to be developed and tested with no AutoCAD present.
    ///
    /// Every database operation follows the same discipline: take a document lock, open a
    /// transaction, do the work, commit, and let the transaction abort on any exception so a failed
    /// batch leaves the drawing untouched rather than half generated.
    /// </remarks>
    public sealed class AcadCadService : ICadService
    {
        private readonly StructureGenContext _context;
        private readonly ILogger _logger;

        /// <summary>Initialises a new instance of the <see cref="AcadCadService"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        public AcadCadService(StructureGenContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = context.Logger;
        }

        /// <inheritdoc />
        public bool HasActiveDocument => AcadApplication.DocumentManager.MdiActiveDocument is not null;

        /// <inheritdoc />
        public string? DocumentName => AcadApplication.DocumentManager.MdiActiveDocument?.Name;

        /// <inheritdoc />
        public Core.Units.LengthUnit? DrawingLengthUnit
        {
            get
            {
                Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
                return document is null ? null : DrawingUnits.ToLengthUnit(document.Database.Insunits);
            }
        }

        /// <inheritdoc />
        public Result<Vec3> PickPoint(string prompt)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<Vec3>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            double scale = ScaleFor(document.Database);
            PromptPointOptions options = new($"\n{prompt}: ") { AllowNone = false };
            PromptPointResult result = document.Editor.GetPoint(options);

            if (result.Status != PromptStatus.OK)
            {
                return Result<Vec3>.Failure("Cad.Cancelled", "Point selection was cancelled.");
            }

            // The point comes back in drawing units; everything inside StructureGen is millimetres.
            Point3d point = result.Value;
            return Result<Vec3>.Success(new Vec3(point.X / scale, point.Y / scale, point.Z / scale));
        }

        /// <inheritdoc />
        public Result<(Placement Placement, decimal LengthMm)> PickSegment(string startPrompt, string endPrompt)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<(Placement, decimal)>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            double scale = ScaleFor(document.Database);
            Editor editor = document.Editor;

            PromptPointResult start = editor.GetPoint(new PromptPointOptions($"\n{startPrompt}: "));
            if (start.Status != PromptStatus.OK)
            {
                return Result<(Placement, decimal)>.Failure("Cad.Cancelled", "Point selection was cancelled.");
            }

            PromptPointOptions endOptions = new($"\n{endPrompt}: ")
            {
                BasePoint = start.Value,
                UseBasePoint = true
            };

            PromptPointResult end = editor.GetPoint(endOptions);
            if (end.Status != PromptStatus.OK)
            {
                return Result<(Placement, decimal)>.Failure("Cad.Cancelled", "Point selection was cancelled.");
            }

            Vec3 from = new(start.Value.X / scale, start.Value.Y / scale, start.Value.Z / scale);
            Vec3 to = new(end.Value.X / scale, end.Value.Y / scale, end.Value.Z / scale);

            if (Vec3.Distance(from, to) <= 1e-6)
            {
                return Result<(Placement, decimal)>.Failure(
                    "Cad.ZeroLength",
                    "The two points are in the same place, so the member has no direction and no length. Pick two distinct points.");
            }

            (Placement placement, decimal lengthMm) = Placement.FromEndPoints(from, to);
            return Result<(Placement, decimal)>.Success((placement, lengthMm));
        }

        /// <inheritdoc />
        public Result<(ElementDefinition Definition, string Handle)> PickElement(string prompt)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<(ElementDefinition, string)>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            PromptEntityOptions options = new($"\n{prompt}: ");
            options.SetRejectMessage("\nThat is not a StructureGen element. Select one created by STRUCTUREGEN.");
            options.AddAllowedClass(typeof(BlockReference), exactMatch: false);

            PromptEntityResult picked = document.Editor.GetEntity(options);
            if (picked.Status != PromptStatus.OK)
            {
                return Result<(ElementDefinition, string)>.Failure("Cad.Cancelled", "Selection was cancelled.");
            }

            using DocumentLock documentLock = document.LockDocument();
            using Transaction transaction = document.Database.TransactionManager.StartTransaction();

            DBObject entity = transaction.GetObject(picked.ObjectId, OpenMode.ForRead);
            Result<DecodedElement> decoded = MetadataStore.ReadElement(transaction, entity);

            if (decoded.IsFailure)
            {
                transaction.Commit();
                return Result<(ElementDefinition, string)>.Failure(decoded.ErrorCode!, decoded.ErrorMessage!);
            }

            string handle = entity.Handle.ToString();
            transaction.Commit();

            return Result<(ElementDefinition, string)>.Success((decoded.Value.Definition, handle));
        }

        /// <inheritdoc />
        public Result<GenerationSummary> Generate(GenerationRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<GenerationSummary>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            if (request.Definitions.Count == 0)
            {
                return Result<GenerationSummary>.Failure(
                    "Cad.NothingToGenerate", "There is nothing in the list to generate. Add at least one item first.");
            }

            ValidationReport report = new();
            Dictionary<Guid, IReadOnlyList<string>> created = new();
            Database database = document.Database;
            double scale = ScaleFor(database);

            WarnAboutUnitlessDrawing(database, report);

            try
            {
                using DocumentLock documentLock = document.LockDocument();
                using Transaction transaction = database.TransactionManager.StartTransaction();

                MetadataStore.EnsureRegisteredApplication(transaction, database);

                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord space =
                    (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);

                BlockBuilder builder = new(scale);
                string stamp = _context.Database.Stamp;
                string version = PluginPaths.PluginVersion;

                foreach (ElementDefinition definition in request.Definitions)
                {
                    List<string> handles = GenerateOne(
                        transaction, database, space, builder, definition, request, stamp, version, report);

                    if (handles.Count > 0)
                    {
                        created[definition.ElementId] = handles;
                    }
                }

                MetadataStore.StampDrawing(transaction, database, stamp, version);
                transaction.Commit();

                GenerationSummary summary = new(created, builder.Created, builder.Reused, report);
                _logger.Information(summary.ToDisplayString());

                if (request.ZoomToResult && created.Count > 0)
                {
                    ZoomExtents(document);
                }

                return Result<GenerationSummary>.Success(summary, report);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                _logger.Error("Generation failed inside AutoCAD.", exception);
                return Result<GenerationSummary>.Failure(
                    "Cad.GenerationFailed",
                    "AutoCAD could not complete the generation, so nothing was added to the drawing. " +
                    $"The reported problem was: {exception.Message}. Technical detail is in the StructureGen log file.");
            }
            catch (InvalidOperationException exception)
            {
                _logger.Error("Generation failed.", exception);
                return Result<GenerationSummary>.Failure(
                    "Cad.GenerationFailed",
                    $"The generation could not be completed, so nothing was added to the drawing: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public Result<string> UpdateElement(string handle, ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<string>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            Database database = document.Database;
            double scale = ScaleFor(database);

            IElementGenerator? generator = _context.Generators.Find(definition.CategoryId);
            if (generator is null)
            {
                return Result<string>.Failure(
                    "Cad.NoGenerator",
                    $"No geometry generator is registered for the category '{definition.CategoryId}'.");
            }

            Result<ElementGeometry> geometry = generator.BuildGeometry(definition);
            if (geometry.IsFailure)
            {
                return Result<string>.Failure(geometry.ErrorCode!, geometry.ErrorMessage!, geometry.Report);
            }

            try
            {
                using DocumentLock documentLock = document.LockDocument();
                using Transaction transaction = database.TransactionManager.StartTransaction();

                if (!TryResolveHandle(database, handle, out ObjectId objectId))
                {
                    return Result<string>.Failure(
                        "Cad.ObjectMissing",
                        "The object being edited is no longer in the drawing. It may have been erased or undone.");
                }

                if (transaction.GetObject(objectId, OpenMode.ForWrite) is not BlockReference reference)
                {
                    return Result<string>.Failure(
                        "Cad.NotAnElement", "The selected object is not a StructureGen element.");
                }

                RepresentationMode representation = definition.Representation;
                BlockBuilder builder = new(scale);
                string stamp = _context.Database.Stamp;
                string version = PluginPaths.PluginVersion;

                string targetName = BlockNaming.BlockNameFor(definition, representation);
                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);

                if (blockTable.Has(targetName))
                {
                    // The new shape already exists in the drawing, so the reference is simply
                    // retargeted. Nothing else that uses either definition is disturbed.
                    reference.BlockTableRecord = blockTable[targetName];
                }
                else if (BlockBuilder.ReferenceCount(transaction, reference.BlockTableRecord) <= 1)
                {
                    // This reference is the only user of its definition, so the definition can be
                    // rebuilt in place and keeps its identity.
                    builder.RebuildDefinition(
                        transaction, database, reference.BlockTableRecord, definition, geometry.Value,
                        representation, stamp, version);
                }
                else
                {
                    // The definition is shared. Rebuilding it would silently reshape every other
                    // member using it, so a new definition is created and only this reference moves.
                    ObjectId newDefinition = builder.ResolveDefinition(
                        transaction, database, definition, geometry.Value, representation, stamp, version);
                    reference.BlockTableRecord = newDefinition;
                }

                if (!string.IsNullOrWhiteSpace(definition.Layer))
                {
                    reference.Layer = LayerManager.ResolveLayer(
                        transaction, database, definition, _context.Settings.LayerPrefix, createIfMissing: true);
                }

                // The element identity is written back exactly as it came in, so an edit can never
                // orphan an object from its history.
                MetadataStore.Write(
                    transaction, database, reference, ElementMetadataCodec.InstanceKey,
                    ElementMetadataCodec.Encode(definition, stamp, version), writeSelectionMarker: true);

                string updatedHandle = reference.Handle.ToString();
                transaction.Commit();

                _logger.Information($"Updated element {definition.ElementId:D} on object {updatedHandle}.");
                return Result<string>.Success(updatedHandle);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                _logger.Error($"Updating object '{handle}' failed inside AutoCAD.", exception);
                return Result<string>.Failure(
                    "Cad.UpdateFailed",
                    "AutoCAD could not update the element, so it has been left as it was. " +
                    $"The reported problem was: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public Result<IReadOnlyList<BomEntry>> HarvestElements(HarvestScope scope)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<IReadOnlyList<BomEntry>>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            try
            {
                return DrawingHarvester.Harvest(document, scope, _context.Mass, _logger);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                _logger.Error("Harvesting elements failed inside AutoCAD.", exception);
                return Result<IReadOnlyList<BomEntry>>.Failure(
                    "Cad.HarvestFailed",
                    $"The drawing could not be read: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public Result<string> InsertBomTable(BomDocument bom, BomTablePlacement placement)
        {
            if (bom is null)
            {
                throw new ArgumentNullException(nameof(bom));
            }

            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
            {
                return Result<string>.Failure("Cad.NoDocument", NoDocumentMessage);
            }

            if (bom.IsEmpty)
            {
                return Result<string>.Failure(
                    "Cad.EmptyBom",
                    "The bill of materials is empty, so there is no table to draw. Generate some elements first.");
            }

            Result<Vec3> point = PickPoint("Pick the top left corner of the schedule");
            if (point.IsFailure)
            {
                return Result<string>.Failure(point.ErrorCode!, point.ErrorMessage!);
            }

            Database database = document.Database;
            double scale = ScaleFor(database);
            Point3d position = new(point.Value.X * scale, point.Value.Y * scale, point.Value.Z * scale);

            try
            {
                using DocumentLock documentLock = document.LockDocument();
                using Transaction transaction = database.TransactionManager.StartTransaction();

                ObjectId spaceId = placement == BomTablePlacement.CurrentLayout
                    ? ResolvePaperSpace(transaction, database)
                    : SymbolUtilityServices.GetBlockModelSpaceId(database);

                BlockTableRecord space = (BlockTableRecord)transaction.GetObject(spaceId, OpenMode.ForWrite);

                Table table = AcadTableWriter.Create(database, bom, position, _context.Settings.BomTextHeight);

                try
                {
                    space.AppendEntity(table);
                    transaction.AddNewlyCreatedDBObject(table, true);
                }
                catch
                {
                    table.Dispose();
                    throw;
                }

                string handle = table.Handle.ToString();
                transaction.Commit();

                _logger.Information($"Inserted a bill of materials table with {bom.Lines.Count} line(s) as object {handle}.");
                return Result<string>.Success(handle);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                _logger.Error("Creating the bill of materials table failed inside AutoCAD.", exception);
                return Result<string>.Failure(
                    "Cad.TableFailed",
                    "AutoCAD could not create the schedule table. This usually means the drawing's table style is " +
                    $"missing or unusable. The reported problem was: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public void WriteMessage(string message)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage("\n" + message);
        }

        /// <inheritdoc />
        public void ShowMessage(string title, string message) =>
            AcadApplication.ShowAlertDialog($"{title}\n\n{message}");

        /// <inheritdoc />
        public void InvokeOnDocumentThread(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // A palette is modeless, so its events arrive outside AutoCAD's command context, where
            // document edits are not permitted. This is the documented way back in.
            _ = AcadApplication.DocumentManager.ExecuteInCommandContextAsync(
                _ =>
                {
                    action();
                    return Task.CompletedTask;
                },
                null);
        }

        /// <summary>Zooms the current view to the drawing's extents.</summary>
        /// <param name="document">The document to zoom.</param>
        public static void ZoomExtents(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            Database database = document.Database;
            database.UpdateExt(true);

            Point3d minimum = database.Extmin;
            Point3d maximum = database.Extmax;

            double width = Math.Abs(maximum.X - minimum.X);
            double height = Math.Abs(maximum.Y - minimum.Y);

            if (width <= 1e-9 || height <= 1e-9)
            {
                return;
            }

            using ViewTableRecord view = document.Editor.GetCurrentView();

            // A tenth of a margin keeps the geometry off the edge of the viewport.
            view.CenterPoint = new Point2d((minimum.X + maximum.X) / 2d, (minimum.Y + maximum.Y) / 2d);
            view.Width = width * 1.1d;
            view.Height = height * 1.1d;

            document.Editor.SetCurrentView(view);
        }

        private List<string> GenerateOne(
            Transaction transaction,
            Database database,
            BlockTableRecord space,
            BlockBuilder builder,
            ElementDefinition definition,
            GenerationRequest request,
            string stamp,
            string version,
            ValidationReport report)
        {
            List<string> handles = new();

            IElementGenerator? generator = _context.Generators.Find(definition.CategoryId);
            if (generator is null)
            {
                report.AddError(
                    "Cad.NoGenerator",
                    $"'{definition.Designation}' was skipped: no geometry generator is registered for the category " +
                    $"'{definition.CategoryId}'. It can still be scheduled but not drawn.",
                    definition.Designation);
                return handles;
            }

            Result<ElementGeometry> geometry = generator.BuildGeometry(definition);
            if (geometry.IsFailure)
            {
                report.AddError(
                    "Cad.GeometryFailed",
                    $"'{definition.Designation}' was skipped: {geometry.ErrorMessage}",
                    definition.Designation);
                return handles;
            }

            RepresentationMode representation = definition.Representation == RepresentationMode.None
                ? RepresentationMode.Solid3D
                : definition.Representation;

            string layer = LayerManager.ResolveLayer(
                transaction, database, definition, request.LayerPrefix, request.CreateLayers);

            ObjectId recordId = builder.ResolveDefinition(
                transaction, database, definition, geometry.Value, representation, stamp, version);

            IReadOnlyList<Placement> placements =
                ElementPlacer.Expand(definition, geometry.Value, request.AutoSpacingMm);

            foreach (Placement placement in placements)
            {
                BlockReference reference = builder.PlaceReference(transaction, space, recordId, placement, layer);

                // Each reference is one physical piece. Writing a quantity of one on every occurrence
                // is what lets the schedule count objects in the drawing rather than trust the number
                // that was typed into the palette, so copying a beam by hand is picked up too.
                ElementDefinition occurrence = definition.With(quantity: 1, placements: new[] { placement });

                MetadataStore.Write(
                    transaction, database, reference, ElementMetadataCodec.InstanceKey,
                    ElementMetadataCodec.Encode(occurrence, stamp, version), writeSelectionMarker: true);

                handles.Add(reference.Handle.ToString());
            }

            return handles;
        }

        private void WarnAboutUnitlessDrawing(Database database, ValidationReport report)
        {
            if (DrawingUnits.IsSupported(database.Insunits))
            {
                return;
            }

            report.AddWarning(
                "Cad.UnitlessDrawing",
                "This drawing does not declare its insertion units, so StructureGen has assumed millimetres. " +
                "If the drawing is in metres the geometry will be a thousand times too large. Set INSUNITS and " +
                "regenerate if that is the case.");

            _logger.Warning($"The drawing's INSUNITS is '{database.Insunits}'; millimetres assumed.");
        }

        private static double ScaleFor(Database database) =>
            DrawingUnits.DrawingUnitsPerMillimetre(database.Insunits);

        private static bool TryResolveHandle(Database database, string handle, out ObjectId objectId)
        {
            objectId = ObjectId.Null;

            if (string.IsNullOrWhiteSpace(handle))
            {
                return false;
            }

            try
            {
                Handle parsed = new(Convert.ToInt64(handle, 16));
                return database.TryGetObjectId(parsed, out objectId) && !objectId.IsNull && !objectId.IsErased;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static ObjectId ResolvePaperSpace(Transaction transaction, Database database)
        {
            LayoutManager manager = LayoutManager.Current;
            string layoutName = manager.CurrentLayout;

            DBDictionary layouts = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);

            if (layouts.Contains(layoutName) &&
                transaction.GetObject(layouts.GetAt(layoutName), OpenMode.ForRead) is Layout layout &&
                !layout.ModelType)
            {
                return layout.BlockTableRecordId;
            }

            // The current layout is model space, which has no paper space of its own. Falling back to
            // model space is better than failing: the user still gets their table.
            return SymbolUtilityServices.GetBlockModelSpaceId(database);
        }

        private const string NoDocumentMessage =
            "There is no drawing open. Open or create a drawing before using StructureGen.";
    }
}
