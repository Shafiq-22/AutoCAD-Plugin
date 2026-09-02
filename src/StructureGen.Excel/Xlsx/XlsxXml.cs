using System;
using System.Globalization;
using System.Text;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>Low-level helpers shared by the SpreadsheetML writer.</summary>
    internal static class XlsxXml
    {
        /// <summary>The XML declaration every part in the package begins with.</summary>
        public const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

        /// <summary>The SpreadsheetML main namespace.</summary>
        public const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        /// <summary>The relationships namespace used inside document parts.</summary>
        public const string DocumentRelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>The package relationships namespace.</summary>
        public const string PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// Escapes text for inclusion in an XML element, and removes the control characters that
        /// XML 1.0 does not permit at all.
        /// </summary>
        /// <param name="text">The text to escape.</param>
        /// <returns>The escaped text.</returns>
        /// <remarks>
        /// The stripping matters in practice: a designation or a note copied out of a PDF section
        /// table can carry a stray control character, and leaving it in produces a workbook that
        /// Excel refuses to open with an unhelpful repair message.
        /// </remarks>
        public static string Escape(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text!.Length + 16);

            foreach (char character in text)
            {
                switch (character)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    case '\'':
                        builder.Append("&apos;");
                        break;
                    case '\t':
                    case '\n':
                    case '\r':
                        builder.Append(character);
                        break;
                    default:
                        if (character >= 0x20 && character != 0xFFFE && character != 0xFFFF)
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>Formats a number for a cell value, round-tripping exactly and without a culture.</summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The formatted number.</returns>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Converts a one-based column index into its spreadsheet letter, for example 28 into <c>AB</c>.</summary>
        /// <param name="columnIndex">The one-based column index.</param>
        /// <returns>The column letters.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The index is less than one.</exception>
        public static string ColumnName(int columnIndex)
        {
            if (columnIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(columnIndex), columnIndex, "Column indexes are one-based.");
            }

            Span<char> letters = stackalloc char[8];
            int position = letters.Length;
            int remaining = columnIndex;

            while (remaining > 0)
            {
                remaining--;
                letters[--position] = (char)('A' + (remaining % 26));
                remaining /= 26;
            }

            return new string(letters[position..]);
        }

        /// <summary>Builds an A1-style cell reference.</summary>
        /// <param name="rowIndex">The one-based row index.</param>
        /// <param name="columnIndex">The one-based column index.</param>
        /// <returns>The reference, for example <c>B12</c>.</returns>
        public static string CellReference(int rowIndex, int columnIndex) =>
            ColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>Builds an A1-style range reference.</summary>
        /// <param name="firstRow">The one-based first row.</param>
        /// <param name="firstColumn">The one-based first column.</param>
        /// <param name="lastRow">The one-based last row.</param>
        /// <param name="lastColumn">The one-based last column.</param>
        /// <returns>The reference, for example <c>A1:M40</c>.</returns>
        public static string RangeReference(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
            CellReference(firstRow, firstColumn) + ":" + CellReference(lastRow, lastColumn);
    }
}
