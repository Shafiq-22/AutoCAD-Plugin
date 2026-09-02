using System;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using StructureGen.Core.Model;

namespace StructureGen.Acad.Internal
{
    /// <summary>Creates and resolves the layers generated elements are placed on.</summary>
    /// <remarks>
    /// An element that names its own layer gets it. One that does not is placed on a layer named
    /// after its category, so a drawing arrives with beams, bolts and plates already separated and a
    /// detailer can freeze the fasteners without selecting anything.
    /// </remarks>
    internal static class LayerManager
    {
        private const short DefaultColourIndex = 7;

        /// <summary>Resolves the layer an element belongs on, creating it when necessary.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="definition">The element being placed.</param>
        /// <param name="prefix">The prefix applied to layers StructureGen creates.</param>
        /// <param name="createIfMissing">Whether a missing layer may be created.</param>
        /// <returns>
        /// The layer name to place the element on. When the layer does not exist and may not be
        /// created, the drawing's current layer is used instead.
        /// </returns>
        public static string ResolveLayer(
            Transaction transaction,
            Database database,
            ElementDefinition definition,
            string prefix,
            bool createIfMissing)
        {
            string requested = string.IsNullOrWhiteSpace(definition.Layer)
                ? DefaultLayerName(definition.CategoryId, prefix)
                : definition.Layer!.Trim();

            string name = SanitiseLayerName(requested);
            LayerTable table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);

            if (table.Has(name))
            {
                return name;
            }

            if (!createIfMissing)
            {
                return LayerNameOf(transaction, database, database.Clayer);
            }

            table.UpgradeOpen();

            LayerTableRecord layer = new()
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, ColourFor(definition.CategoryId))
            };

            table.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);

            return name;
        }

        /// <summary>Builds the automatic layer name for a category.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="prefix">The prefix applied to layers StructureGen creates.</param>
        /// <returns>The layer name.</returns>
        public static string DefaultLayerName(string categoryId, string prefix) =>
            SanitiseLayerName((prefix ?? string.Empty) + (categoryId ?? "element"));

        /// <summary>
        /// Reduces a proposed layer name to one AutoCAD accepts.
        /// </summary>
        /// <param name="name">The proposed name.</param>
        /// <returns>The sanitised name, never empty.</returns>
        public static string SanitiseLayerName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "SG-element";
            }

            StringBuilder builder = new(name!.Length);

            foreach (char character in name)
            {
                bool invalid = character is '<' or '>' or '/' or '\\' or '"' or ':' or ';' or '?' or '*'
                                          or '|' or ',' or '=' or '`' or '\'';

                builder.Append(invalid || char.IsControl(character) ? '-' : character);
            }

            string result = builder.ToString().Trim();
            if (result.Length == 0)
            {
                return "SG-element";
            }

            return result.Length <= 255 ? result : result.Substring(0, 255);
        }

        private static string LayerNameOf(Transaction transaction, Database database, ObjectId layerId)
        {
            try
            {
                return ((LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead)).Name;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return "0";
            }
        }

        private static short ColourFor(string categoryId) => categoryId switch
        {
            ElementCategory.ISection or ElementCategory.HSection => 5,
            ElementCategory.Channel => 4,
            ElementCategory.Angle => 3,
            ElementCategory.TSection => 6,
            ElementCategory.Pipe => 2,
            ElementCategory.RectangularHollowSection or ElementCategory.SquareHollowSection => 30,
            ElementCategory.Plate => 8,
            ElementCategory.FlatBar or ElementCategory.RoundBar => 9,
            ElementCategory.Bolt or ElementCategory.Nut or ElementCategory.Washer => 1,
            _ => DefaultColourIndex
        };
    }
}
