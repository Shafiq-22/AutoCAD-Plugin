using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>A worksheet being built.</summary>
    public sealed class XlsxWorksheet
    {
        private readonly SortedDictionary<int, SortedDictionary<int, XlsxCell>> _cells = new();
        private readonly Dictionary<int, double> _columnWidths = new();
        private readonly List<(int FirstRow, int FirstColumn, int LastRow, int LastColumn)> _mergedRanges = new();

        internal XlsxWorksheet(string name) => Name = name;

        /// <summary>Gets the sheet name as it appears on the tab.</summary>
        public string Name { get; }

        /// <summary>Gets the one-based index of the row the next call to <see cref="AddRow"/> will write.</summary>
        public int NextRowIndex { get; private set; } = 1;

        /// <summary>Gets the one-based index of the last row that holds a cell, or zero when empty.</summary>
        public int LastRowIndex => _cells.Count == 0 ? 0 : _cells.Keys.Max();

        /// <summary>Gets the one-based index of the last column that holds a cell, or zero when empty.</summary>
        public int LastColumnIndex => _cells.Count == 0
            ? 0
            : _cells.Values.Where(row => row.Count > 0).Select(row => row.Keys.Max()).DefaultIfEmpty(0).Max();

        /// <summary>Gets or sets the number of leading rows kept visible when the sheet is scrolled.</summary>
        public int FrozenRows { get; set; }

        /// <summary>Gets or sets the number of leading columns kept visible when the sheet is scrolled.</summary>
        public int FrozenColumns { get; set; }

        /// <summary>Gets or sets the range the auto-filter drop-downs cover, or <see langword="null"/> for none.</summary>
        public (int FirstRow, int FirstColumn, int LastRow, int LastColumn)? AutoFilterRange { get; set; }

        /// <summary>Sets the width of a column, in the spreadsheet's character-width units.</summary>
        /// <param name="columnIndex">The one-based column index.</param>
        /// <param name="width">The width.</param>
        public void SetColumnWidth(int columnIndex, double width)
        {
            if (columnIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Column indexes are one-based.");
            }

            _columnWidths[columnIndex] = width;
        }

        /// <summary>Sets the widths of consecutive columns starting at column one.</summary>
        /// <param name="widths">The widths, in order.</param>
        public void SetColumnWidths(params double[] widths)
        {
            if (widths is null)
            {
                throw new ArgumentNullException(nameof(widths));
            }

            for (int index = 0; index < widths.Length; index++)
            {
                SetColumnWidth(index + 1, widths[index]);
            }
        }

        /// <summary>Merges a rectangular range of cells.</summary>
        /// <param name="firstRow">The one-based first row.</param>
        /// <param name="firstColumn">The one-based first column.</param>
        /// <param name="lastRow">The one-based last row.</param>
        /// <param name="lastColumn">The one-based last column.</param>
        public void Merge(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
            _mergedRanges.Add((firstRow, firstColumn, lastRow, lastColumn));

        /// <summary>Starts a new row immediately below the last one written.</summary>
        /// <returns>A writer for the new row.</returns>
        public XlsxRow AddRow() => new(this, NextRowIndex++);

        /// <summary>Leaves one or more rows empty.</summary>
        /// <param name="count">How many rows to skip.</param>
        public void SkipRows(int count = 1) => NextRowIndex += Math.Max(0, count);

        /// <summary>Writes a cell directly.</summary>
        /// <param name="rowIndex">The one-based row index.</param>
        /// <param name="columnIndex">The one-based column index.</param>
        /// <param name="cell">The cell to write.</param>
        internal void SetCell(int rowIndex, int columnIndex, XlsxCell cell)
        {
            if (rowIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex), rowIndex, "Row indexes are one-based.");
            }

            if (columnIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Column indexes are one-based.");
            }

            if (!_cells.TryGetValue(rowIndex, out SortedDictionary<int, XlsxCell>? row))
            {
                row = new SortedDictionary<int, XlsxCell>();
                _cells[rowIndex] = row;
            }

            row[columnIndex] = cell;

            if (rowIndex >= NextRowIndex)
            {
                NextRowIndex = rowIndex + 1;
            }
        }

        /// <summary>Renders the sheet as the XML of an <c>xl/worksheets/sheetN.xml</c> part.</summary>
        /// <param name="sharedStrings">The workbook's shared string table, which this call adds to.</param>
        /// <returns>The XML.</returns>
        internal string ToXml(XlsxSharedStrings sharedStrings)
        {
            StringBuilder builder = new(4096);
            builder.Append(XlsxXml.Declaration);
            builder.Append("<worksheet xmlns=\"").Append(XlsxXml.SpreadsheetNamespace)
                   .Append("\" xmlns:r=\"").Append(XlsxXml.DocumentRelationshipNamespace).Append("\">");

            int lastRow = Math.Max(1, LastRowIndex);
            int lastColumn = Math.Max(1, LastColumnIndex);
            builder.Append("<dimension ref=\"").Append(XlsxXml.RangeReference(1, 1, lastRow, lastColumn)).Append("\"/>");

            AppendSheetViews(builder);

            builder.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            AppendColumns(builder);
            AppendSheetData(builder, sharedStrings);

            // The schema fixes this order: the auto-filter must precede merged cells.
            if (AutoFilterRange is { } filter)
            {
                builder.Append("<autoFilter ref=\"")
                       .Append(XlsxXml.RangeReference(filter.FirstRow, filter.FirstColumn, filter.LastRow, filter.LastColumn))
                       .Append("\"/>");
            }

            if (_mergedRanges.Count > 0)
            {
                builder.Append("<mergeCells count=\"").Append(_mergedRanges.Count).Append("\">");
                foreach ((int firstRow, int firstColumn, int mergedLastRow, int mergedLastColumn) in _mergedRanges)
                {
                    builder.Append("<mergeCell ref=\"")
                           .Append(XlsxXml.RangeReference(firstRow, firstColumn, mergedLastRow, mergedLastColumn))
                           .Append("\"/>");
                }

                builder.Append("</mergeCells>");
            }

            builder.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private void AppendSheetViews(StringBuilder builder)
        {
            builder.Append("<sheetViews><sheetView workbookViewId=\"0\"");
            builder.Append(">");

            if (FrozenRows > 0 || FrozenColumns > 0)
            {
                string activePane = FrozenColumns > 0 && FrozenRows > 0
                    ? "bottomRight"
                    : FrozenColumns > 0 ? "topRight" : "bottomLeft";

                builder.Append("<pane");
                if (FrozenColumns > 0)
                {
                    builder.Append(" xSplit=\"").Append(FrozenColumns).Append('"');
                }

                if (FrozenRows > 0)
                {
                    builder.Append(" ySplit=\"").Append(FrozenRows).Append('"');
                }

                builder.Append(" topLeftCell=\"")
                       .Append(XlsxXml.CellReference(FrozenRows + 1, FrozenColumns + 1))
                       .Append("\" activePane=\"").Append(activePane)
                       .Append("\" state=\"frozen\"/>");
                builder.Append("<selection pane=\"").Append(activePane).Append("\"/>");
            }

            builder.Append("</sheetView></sheetViews>");
        }

        private void AppendColumns(StringBuilder builder)
        {
            if (_columnWidths.Count == 0)
            {
                return;
            }

            builder.Append("<cols>");
            foreach (KeyValuePair<int, double> pair in _columnWidths.OrderBy(p => p.Key))
            {
                builder.Append("<col min=\"").Append(pair.Key)
                       .Append("\" max=\"").Append(pair.Key)
                       .Append("\" width=\"").Append(pair.Value.ToString("0.##", CultureInfo.InvariantCulture))
                       .Append("\" customWidth=\"1\"/>");
            }

            builder.Append("</cols>");
        }

        private void AppendSheetData(StringBuilder builder, XlsxSharedStrings sharedStrings)
        {
            builder.Append("<sheetData>");

            foreach (KeyValuePair<int, SortedDictionary<int, XlsxCell>> row in _cells)
            {
                if (row.Value.Count == 0)
                {
                    continue;
                }

                builder.Append("<row r=\"").Append(row.Key).Append("\">");

                foreach (KeyValuePair<int, XlsxCell> cell in row.Value)
                {
                    AppendCell(builder, row.Key, cell.Key, cell.Value, sharedStrings);
                }

                builder.Append("</row>");
            }

            builder.Append("</sheetData>");
        }

        private static void AppendCell(
            StringBuilder builder, int rowIndex, int columnIndex, XlsxCell cell, XlsxSharedStrings sharedStrings)
        {
            builder.Append("<c r=\"").Append(XlsxXml.CellReference(rowIndex, columnIndex)).Append('"');

            if (cell.Style != XlsxStyle.Default)
            {
                builder.Append(" s=\"").Append((int)cell.Style).Append('"');
            }

            switch (cell.Kind)
            {
                case XlsxCellKind.Text:
                    builder.Append(" t=\"s\"><v>").Append(sharedStrings.Index(cell.Text!)).Append("</v></c>");
                    break;

                case XlsxCellKind.Number:
                    builder.Append("><v>").Append(XlsxXml.Number(cell.Number)).Append("</v></c>");
                    break;

                case XlsxCellKind.Boolean:
                    builder.Append(" t=\"b\"><v>").Append(cell.Number > 0d ? 1 : 0).Append("</v></c>");
                    break;

                default:
                    builder.Append("/>");
                    break;
            }
        }
    }
}
