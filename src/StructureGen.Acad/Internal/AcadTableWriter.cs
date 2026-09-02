using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructureGen.Bom;
using StructureGen.Core.Units;

namespace StructureGen.Acad.Internal
{
    /// <summary>Draws a bill of materials as a native AutoCAD table.</summary>
    /// <remarks>
    /// A native table is used rather than assembled text and lines because it stays editable,
    /// respects the drawing's table style, and can be selected, formatted and printed like any other
    /// annotation. The table is a snapshot: it records what the schedule said when it was drawn, and
    /// running the command again produces a fresh one.
    /// </remarks>
    internal static class AcadTableWriter
    {
        private static readonly string[] Headers =
        {
            "Item", "Category", "Standard", "Profile", "Dimensions (mm)",
            "Length", "Qty", "Unit wt (kg/m)", "Total wt (kg)", "Material"
        };

        private static readonly double[] ColumnWidths = { 12d, 32d, 22d, 40d, 46d, 20d, 12d, 24d, 26d, 24d };

        /// <summary>Creates the table entity.</summary>
        /// <param name="database">The drawing database.</param>
        /// <param name="document">The schedule to draw.</param>
        /// <param name="position">The top left corner, in drawing units.</param>
        /// <param name="textHeight">The text height, in drawing units.</param>
        /// <returns>The table. The caller owns it until it is appended to a space.</returns>
        public static Table Create(Database database, BomDocument document, Point3d position, double textHeight)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<string[]> rows = BuildRows(document);
            int columnCount = Headers.Length;

            // One title row, one header row, the data, one grand total row, then any caveats.
            IReadOnlyList<string> caveats = document.Caveats();
            int rowCount = 2 + rows.Count + 1 + caveats.Count;

            Table table = new();

            try
            {
                table.SetDatabaseDefaults(database);
                table.TableStyle = database.Tablestyle;
                table.SetSize(rowCount, columnCount);
                table.Position = position;

                double scale = Math.Max(0.01d, textHeight);

                for (int column = 0; column < columnCount; column++)
                {
                    table.Columns[column].Width = ColumnWidths[column] * scale / 2.5d;
                }

                WriteTitle(table, document, columnCount, scale);
                WriteHeader(table, columnCount, scale, document.Options.LengthUnit);
                WriteRows(table, rows, scale);
                WriteTotals(table, document, rows.Count, columnCount, scale);
                WriteCaveats(table, caveats, rows.Count, columnCount, scale);

                table.GenerateLayout();
                return table;
            }
            catch
            {
                table.Dispose();
                throw;
            }
        }

        private static List<string[]> BuildRows(BomDocument document)
        {
            LengthUnit lengthUnit = document.Options.LengthUnit;
            List<string[]> rows = new(document.Lines.Count);

            foreach (BomLine line in document.Lines)
            {
                rows.Add(new[]
                {
                    line.ItemNumber.ToString(CultureInfo.InvariantCulture),
                    line.CategoryName,
                    line.StandardId,
                    line.Designation + (line.IsCustom ? " (non-standard)" : string.Empty),
                    line.DimensionSummary,
                    Format(line.LengthIn(lengthUnit), 3),
                    line.Quantity.ToString(CultureInfo.InvariantCulture),
                    Format(line.UnitWeightKgPerM, 2),
                    Format(line.TotalMassKg, 2),
                    line.Material ?? "-"
                });
            }

            return rows;
        }

        private static void WriteTitle(Table table, BomDocument document, int columnCount, double scale)
        {
            table.Cells[0, 0].TextString = document.Options.Title;
            table.Cells[0, 0].TextHeight = scale * 1.6d;
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.MergeCells(CellRange.Create(table, 0, 0, 0, columnCount - 1));
        }

        private static void WriteHeader(Table table, int columnCount, double scale, LengthUnit lengthUnit)
        {
            string lengthAbbreviation = UnitConverter.Abbreviation(lengthUnit);

            for (int column = 0; column < columnCount; column++)
            {
                string caption = Headers[column] == "Length"
                    ? $"Length ({lengthAbbreviation})"
                    : Headers[column];

                table.Cells[1, column].TextString = caption;
                table.Cells[1, column].TextHeight = scale;
                table.Cells[1, column].Alignment = CellAlignment.MiddleCenter;
            }
        }

        private static void WriteRows(Table table, List<string[]> rows, double scale)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                string[] values = rows[index];
                int row = index + 2;

                for (int column = 0; column < values.Length; column++)
                {
                    table.Cells[row, column].TextString = values[column];
                    table.Cells[row, column].TextHeight = scale;
                    table.Cells[row, column].Alignment = column is 0 or 6
                        ? CellAlignment.MiddleCenter
                        : column is 5 or 7 or 8
                            ? CellAlignment.MiddleRight
                            : CellAlignment.MiddleLeft;
                }
            }
        }

        private static void WriteTotals(Table table, BomDocument document, int dataRowCount, int columnCount, double scale)
        {
            int row = dataRowCount + 2;

            table.Cells[row, 0].TextString = "GRAND TOTAL";
            table.Cells[row, 0].TextHeight = scale;
            table.Cells[row, 0].Alignment = CellAlignment.MiddleLeft;
            table.MergeCells(CellRange.Create(table, row, 0, row, 5));

            table.Cells[row, 6].TextString = document.Totals.TotalPieces.ToString(CultureInfo.InvariantCulture);
            table.Cells[row, 6].TextHeight = scale;
            table.Cells[row, 6].Alignment = CellAlignment.MiddleCenter;

            table.Cells[row, 7].TextString = string.Empty;

            table.Cells[row, 8].TextString =
                document.Totals.TotalMassKg.ToString("N2", CultureInfo.InvariantCulture) +
                $" kg ({document.Totals.TotalMassTonnes.ToString("N3", CultureInfo.InvariantCulture)} t)";
            table.Cells[row, 8].TextHeight = scale;
            table.Cells[row, 8].Alignment = CellAlignment.MiddleRight;
            table.MergeCells(CellRange.Create(table, row, 8, row, columnCount - 1));
        }

        private static void WriteCaveats(
            Table table, IReadOnlyList<string> caveats, int dataRowCount, int columnCount, double scale)
        {
            for (int index = 0; index < caveats.Count; index++)
            {
                int row = dataRowCount + 3 + index;

                table.Cells[row, 0].TextString = caveats[index];
                table.Cells[row, 0].TextHeight = scale * 0.8d;
                table.Cells[row, 0].Alignment = CellAlignment.MiddleLeft;
                table.MergeCells(CellRange.Create(table, row, 0, row, columnCount - 1));
            }
        }

        /// <summary>
        /// Formats a number, or a dash when the value is unknown.
        /// </summary>
        /// <param name="value">The value, or <see langword="null"/> when it is unknown.</param>
        /// <param name="decimals">How many decimal places to show.</param>
        /// <returns>The formatted text.</returns>
        /// <remarks>
        /// An unknown weight is a dash, never a zero. Writing a zero would let someone add the
        /// column up by hand and arrive at a total that quietly omitted it.
        /// </remarks>
        private static string Format(decimal? value, int decimals) =>
            value.HasValue
                ? value.Value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : "-";
    }
}
