using System;
using System.Collections.Generic;
using System.Text;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>The workbook's shared string table.</summary>
    /// <remarks>
    /// A schedule repeats the same designations, materials and layer names many times over, so
    /// interning them keeps the file small and matches how a spreadsheet application writes one.
    /// </remarks>
    internal sealed class XlsxSharedStrings
    {
        private readonly Dictionary<string, int> _indexes = new(StringComparer.Ordinal);
        private readonly List<string> _values = new();

        /// <summary>Gets the number of distinct strings interned.</summary>
        public int Count => _values.Count;

        /// <summary>Interns a string and returns its index.</summary>
        /// <param name="value">The string to intern.</param>
        /// <returns>The zero-based index in the table.</returns>
        public int Index(string value)
        {
            if (_indexes.TryGetValue(value, out int existing))
            {
                return existing;
            }

            int index = _values.Count;
            _values.Add(value);
            _indexes[value] = index;
            return index;
        }

        /// <summary>Renders the table as the XML of the <c>xl/sharedStrings.xml</c> part.</summary>
        /// <returns>The XML.</returns>
        public string ToXml()
        {
            StringBuilder builder = new(1024);
            builder.Append(XlsxXml.Declaration);
            builder.Append("<sst xmlns=\"").Append(XlsxXml.SpreadsheetNamespace)
                   .Append("\" count=\"").Append(_values.Count)
                   .Append("\" uniqueCount=\"").Append(_values.Count).Append("\">");

            foreach (string value in _values)
            {
                // Leading and trailing whitespace is significant and must be preserved explicitly.
                builder.Append("<si><t xml:space=\"preserve\">").Append(XlsxXml.Escape(value)).Append("</t></si>");
            }

            builder.Append("</sst>");
            return builder.ToString();
        }
    }
}
