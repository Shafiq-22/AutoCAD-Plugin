using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructureGen.Core.Geometry;
using StructureGen.Core.Results;
using StructureGen.Geometry.Drawing;
using StructureGen.Services;

namespace StructureGen.Acad.Internal
{
    /// <summary>
    /// Turns a composed plan drawing into AutoCAD entities: one block definition holding a
    /// polyline per line and a text per label, on layers that carry the right linetypes.
    /// </summary>
    /// <remarks>
    /// The plan goes into a block so it arrives as one object that can be moved, copied to a layout
    /// or erased and redrawn in a single step. It carries no StructureGen marker, so the bill of
    /// materials never counts it. Exploding it leaves ordinary polylines and text.
    /// </remarks>
    internal static class PlanWriter
    {
        private const string HiddenLinetype = "HIDDEN";
        private const string CentreLinetype = "CENTER";

        /// <summary>Creates a block definition holding the plan.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="drawing">The plan.</param>
        /// <param name="basePointMm">The plan point that becomes the block's base point.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <param name="layerPrefix">The prefix for the layers the plan is drawn on.</param>
        /// <param name="report">Collects anything worth telling the user.</param>
        /// <returns>The definition and its name.</returns>
        public static (ObjectId DefinitionId, string Name) CreateDefinition(
            Transaction transaction,
            Database database,
            PlanDrawing drawing,
            Vec2 basePointMm,
            double scale,
            string layerPrefix,
            ValidationReport report)
        {
            bool metric = database.Measurement == MeasurementValue.Metric;
            string linetypeFile = metric ? "acadiso.lin" : "acad.lin";

            // Dash lengths in the shipped linetype files, in that file's own units, used to turn
            // the size of each member into a linetype scale that suits it.
            double hiddenDash = metric ? 6.35d : 0.25d;
            double centreDash = metric ? 31.75d : 1.25d;

            ObjectId hiddenType = EnsureLinetype(transaction, database, HiddenLinetype, linetypeFile, report);
            ObjectId centreType = EnsureLinetype(transaction, database, CentreLinetype, linetypeFile, report);

            string visibleLayer = EnsureLayer(transaction, database, layerPrefix + "PLAN-VISIBLE", 7, ObjectId.Null);
            string hiddenLayer = EnsureLayer(transaction, database, layerPrefix + "PLAN-HIDDEN", 8, hiddenType);
            string centreLayer = EnsureLayer(transaction, database, layerPrefix + "PLAN-CENTRE", 1, centreType);
            string textLayer = EnsureLayer(transaction, database, layerPrefix + "PLAN-TEXT", 2, ObjectId.Null);

            BlockTable table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
            string name = UniqueName(table, drawing.Layout == PlanLayout.Items ? "SGPLAN_ITEMS" : "SGPLAN_ARRANGEMENT");

            BlockTableRecord record = new()
            {
                Name = name,
                Origin = Point3d.Origin,
                Explodable = true,
                BlockScaling = BlockScaling.Any
            };

            ObjectId recordId = table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            double globalScale = database.Ltscale > 1e-12 ? database.Ltscale : 1d;

            foreach (PlanCurve curve in drawing.Curves)
            {
                Polyline polyline = new(curve.Vertices.Count);

                try
                {
                    for (int index = 0; index < curve.Vertices.Count; index++)
                    {
                        ProfileVertex vertex = curve.Vertices[index];
                        polyline.AddVertexAt(
                            index,
                            new Point2d((vertex.Point.X - basePointMm.X) * scale, (vertex.Point.Y - basePointMm.Y) * scale),
                            vertex.Bulge,
                            0d,
                            0d);
                    }

                    polyline.Closed = curve.Closed;
                    polyline.SetDatabaseDefaults(database);

                    record.AppendEntity(polyline);
                    transaction.AddNewlyCreatedDBObject(polyline, true);

                    switch (curve.Kind)
                    {
                        case PlanLineKind.Hidden:
                            polyline.Layer = hiddenLayer;
                            polyline.LinetypeScale = LinetypeScale(curve.PatternSizeMm / 15d, hiddenDash, scale, globalScale);
                            break;

                        case PlanLineKind.Centre:
                            polyline.Layer = centreLayer;
                            polyline.LinetypeScale = LinetypeScale(curve.PatternSizeMm / 4d, centreDash, scale, globalScale);
                            break;

                        default:
                            polyline.Layer = visibleLayer;
                            break;
                    }
                }
                catch
                {
                    if (polyline.ObjectId.IsNull)
                    {
                        polyline.Dispose();
                    }

                    throw;
                }
            }

            foreach (PlanText label in drawing.Texts)
            {
                DBText text = new();

                try
                {
                    Point3d position = new(
                        (label.Position.X - basePointMm.X) * scale, (label.Position.Y - basePointMm.Y) * scale, 0d);

                    text.SetDatabaseDefaults(database);
                    text.TextString = label.Text;
                    text.Height = label.HeightMm * scale;
                    text.Rotation = label.Angle;
                    text.Position = position;

                    if (label.Alignment == PlanTextAlignment.BottomCentre)
                    {
                        text.HorizontalMode = TextHorizontalMode.TextCenter;
                        text.VerticalMode = TextVerticalMode.TextBottom;
                        text.AlignmentPoint = position;
                    }

                    record.AppendEntity(text);
                    transaction.AddNewlyCreatedDBObject(text, true);
                    text.Layer = textLayer;

                    if (label.Alignment != PlanTextAlignment.BottomLeft)
                    {
                        // A justified text works out its own position only once it belongs to a
                        // database; without this it is drawn at the origin until the next regen.
                        text.AdjustAlignment(database);
                    }
                }
                catch
                {
                    if (text.ObjectId.IsNull)
                    {
                        text.Dispose();
                    }

                    throw;
                }
            }

            return (recordId, name);
        }

        private static double LinetypeScale(double wantedDashMm, double fileDash, double scale, double globalScale)
        {
            double wanted = wantedDashMm * scale;
            double result = wanted / (fileDash * globalScale);
            return Math.Max(1e-3, result);
        }

        private static string UniqueName(BlockTable table, string stem)
        {
            for (int index = 1; ; index++)
            {
                string candidate = $"{stem}_{index}";
                if (!table.Has(candidate))
                {
                    return candidate;
                }
            }
        }

        private static ObjectId EnsureLinetype(
            Transaction transaction, Database database, string name, string file, ValidationReport report)
        {
            LinetypeTable table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);

            if (table.Has(name))
            {
                return table[name];
            }

            try
            {
                database.LoadLineTypeFile(name, file);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                report.AddWarning(
                    "Plan.LinetypeMissing",
                    $"The {name} linetype could not be loaded from {file} ({exception.Message}), so those lines are " +
                    "drawn continuous. They are still on their own layer and colour; load the linetype and assign it " +
                    "to the layer to fix this.");
                return ObjectId.Null;
            }

            table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
            return table.Has(name) ? table[name] : ObjectId.Null;
        }

        private static string EnsureLayer(
            Transaction transaction, Database database, string requested, short colour, ObjectId linetype)
        {
            string name = LayerManager.SanitiseLayerName(requested);
            LayerTable table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);

            if (table.Has(name))
            {
                // A layer that already exists is the user's to style; it is used as it is.
                return name;
            }

            table.UpgradeOpen();

            LayerTableRecord layer = new()
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colour)
            };

            if (!linetype.IsNull)
            {
                layer.LinetypeObjectId = linetype;
            }

            table.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);

            return name;
        }
    }
}
