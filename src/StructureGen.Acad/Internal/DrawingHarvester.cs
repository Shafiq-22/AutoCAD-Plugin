using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Services;

namespace StructureGen.Acad.Internal
{
    /// <summary>
    /// Collects the StructureGen elements that actually exist in a drawing.
    /// </summary>
    /// <remarks>
    /// The schedule is built from this, not from the palette's working list, and the difference
    /// matters. A member the detailer erased by hand disappears from the next schedule; one they
    /// copied with the COPY command appears on it, carrying its parameters, because the metadata
    /// travelled with the copy. The palette's list is only ever a staging area for what to create.
    /// </remarks>
    internal static class DrawingHarvester
    {
        /// <summary>Collects elements from a drawing.</summary>
        /// <param name="document">The drawing to read.</param>
        /// <param name="scope">Where to look.</param>
        /// <param name="mass">Computes the weight of each occurrence.</param>
        /// <param name="logger">Receives technical detail about objects that could not be read.</param>
        /// <returns>One entry per generated object.</returns>
        public static Result<IReadOnlyList<BomEntry>> Harvest(
            Document document, HarvestScope scope, MassCalculator mass, ILogger logger)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            Result<List<ObjectId>> candidates = CollectCandidates(document, scope);
            if (candidates.IsFailure)
            {
                return Result<IReadOnlyList<BomEntry>>.Failure(candidates.ErrorCode!, candidates.ErrorMessage!);
            }

            List<BomEntry> entries = new(candidates.Value.Count);
            int unreadable = 0;

            using DocumentLock documentLock = document.LockDocument();
            using Transaction transaction = document.Database.TransactionManager.StartTransaction();

            foreach (ObjectId objectId in candidates.Value)
            {
                if (objectId.IsNull || objectId.IsErased)
                {
                    continue;
                }

                DBObject entity = transaction.GetObject(objectId, OpenMode.ForRead);
                Result<DecodedElement> decoded = MetadataStore.ReadElement(transaction, entity);

                if (decoded.IsFailure)
                {
                    unreadable++;
                    continue;
                }

                ElementDefinition definition = decoded.Value.Definition;
                MassProperties properties = mass.Compute(definition);

                entries.Add(new BomEntry(
                    definition,
                    properties,
                    entity.Handle.ToString(),
                    entity is Entity drawable ? drawable.Layer : definition.Layer,
                    SpaceNameOf(transaction, objectId)));
            }

            transaction.Commit();

            if (unreadable > 0)
            {
                logger.Warning(
                    $"{unreadable} object(s) carried the StructureGen marker but no readable parameters and were " +
                    "left out of the schedule.");
            }

            logger.Information($"Harvested {entries.Count} element(s) from the drawing ({scope}).");
            return Result<IReadOnlyList<BomEntry>>.Success(entries);
        }

        private static Result<List<ObjectId>> CollectCandidates(Document document, HarvestScope scope)
        {
            Editor editor = document.Editor;
            SelectionFilter filter = MetadataStore.CreateSelectionFilter();

            PromptSelectionResult selection = scope switch
            {
                // A selection filter matching the extended entity data marker is what makes this a
                // single fast pass instead of opening every entity in the drawing and inspecting it.
                HarvestScope.Selection => editor.GetSelection(filter),
                HarvestScope.EntireDrawing => editor.SelectAll(filter),
                _ => editor.SelectAll(filter)
            };

            if (selection.Status == PromptStatus.Cancel)
            {
                return Result<List<ObjectId>>.Failure("Cad.Cancelled", "Selection was cancelled.");
            }

            if (selection.Status != PromptStatus.OK || selection.Value is null)
            {
                return Result<List<ObjectId>>.Success(new List<ObjectId>());
            }

            List<ObjectId> ids = new(selection.Value.Count);
            foreach (SelectedObject? selected in selection.Value)
            {
                if (selected is not null)
                {
                    ids.Add(selected.ObjectId);
                }
            }

            return Result<List<ObjectId>>.Success(ids);
        }

        private static string? SpaceNameOf(Transaction transaction, ObjectId objectId)
        {
            try
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
                {
                    return null;
                }

                BlockTableRecord owner = (BlockTableRecord)transaction.GetObject(entity.BlockId, OpenMode.ForRead);
                return owner.Name;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return null;
            }
        }
    }
}
