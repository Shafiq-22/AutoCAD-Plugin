using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using StructureGen.Core.Tabular;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>
    /// Reads a worksheet out of an <c>.xlsx</c> file into a plain table of text cells.
    /// </summary>
    /// <remarks>
    /// This exists so that a user can hand over their section data as the spreadsheet they already
    /// keep it in, rather than converting it by hand first. Only what the importer needs is read:
    /// cell values as text, with the column positions preserved so that blank cells do not shift a
    /// row. Formatting, formulas, charts and everything else in the package are ignored.
    /// </remarks>
    public static class XlsxReader
    {
        /// <summary>Lists the worksheet names in a workbook, in tab order.</summary>
        /// <param name="path">The workbook to inspect.</param>
        /// <returns>The sheet names.</returns>
        public static IReadOnlyList<string> SheetNames(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            return ReadSheetEntries(archive).Select(s => s.Name).ToList();
        }

        /// <summary>Reads a worksheet.</summary>
        /// <param name="path">The workbook to read.</param>
        /// <param name="sheetName">
        /// The worksheet to read. When <see langword="null"/> the first worksheet is used.
        /// </param>
        /// <returns>The worksheet as a table of text cells, the first row taken as the header.</returns>
        /// <exception cref="InvalidOperationException">The workbook has no such worksheet.</exception>
        public static ITabularSource Read(string path, string? sheetName = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            using ZipArchive archive = ZipFile.OpenRead(path);

            List<(string Name, string Target)> sheets = ReadSheetEntries(archive);
            if (sheets.Count == 0)
            {
                throw new InvalidOperationException($"The workbook '{Path.GetFileName(path)}' contains no worksheets.");
            }

            (string Name, string Target) chosen = sheetName is null
                ? sheets[0]
                : sheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));

            if (chosen.Name is null)
            {
                throw new InvalidOperationException(
                    $"The workbook '{Path.GetFileName(path)}' has no worksheet named '{sheetName}'. " +
                    $"It contains: {string.Join(", ", sheets.Select(s => s.Name))}.");
            }

            ZipArchiveEntry? entry = archive.GetEntry(chosen.Target)
                                    ?? archive.GetEntry(chosen.Target.TrimStart('/'));
            if (entry is null)
            {
                throw new InvalidOperationException(
                    $"The worksheet part '{chosen.Target}' is missing from '{Path.GetFileName(path)}'.");
            }

            List<string> sharedStrings = ReadSharedStrings(archive);
            List<List<string>> rows = ReadSheet(entry, sharedStrings);

            string name = $"{Path.GetFileName(path)}[{chosen.Name}]";
            if (rows.Count == 0)
            {
                return new TabularSource(name, Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
            }

            List<string> headers = rows[0];
            List<IReadOnlyList<string>> data = new(rows.Count - 1);
            for (int index = 1; index < rows.Count; index++)
            {
                data.Add(rows[index]);
            }

            return new TabularSource(name, headers, data);
        }

        private static List<(string Name, string Target)> ReadSheetEntries(ZipArchive archive)
        {
            List<(string Name, string RelationshipId)> sheets = new();
            Dictionary<string, string> relationships = new(StringComparer.Ordinal);

            ZipArchiveEntry? workbook = archive.GetEntry("xl/workbook.xml");
            if (workbook is not null)
            {
                using Stream stream = workbook.Open();
                using XmlReader reader = XmlReader.Create(stream, SafeSettings());

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
                    {
                        string? name = reader.GetAttribute("name");
                        string? id = reader.GetAttribute("id", XlsxXml.DocumentRelationshipNamespace);
                        if (name is not null && id is not null)
                        {
                            sheets.Add((name, id));
                        }
                    }
                }
            }

            ZipArchiveEntry? relationshipPart = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (relationshipPart is not null)
            {
                using Stream stream = relationshipPart.Open();
                using XmlReader reader = XmlReader.Create(stream, SafeSettings());

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
                    {
                        string? id = reader.GetAttribute("Id");
                        string? target = reader.GetAttribute("Target");
                        if (id is not null && target is not null)
                        {
                            relationships[id] = NormaliseTarget(target);
                        }
                    }
                }
            }

            return sheets
                .Where(s => relationships.ContainsKey(s.RelationshipId))
                .Select(s => (s.Name, relationships[s.RelationshipId]))
                .ToList();
        }

        private static string NormaliseTarget(string target)
        {
            string trimmed = target.Replace('\\', '/').TrimStart('/');
            return trimmed.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? trimmed : "xl/" + trimmed;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> values = new();
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return values;
            }

            using Stream stream = entry.Open();
            using XmlReader reader = XmlReader.Create(stream, SafeSettings());

            StringBuilder current = new();
            bool inItem = false;

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element when reader.LocalName == "si":
                        current.Clear();
                        inItem = true;
                        if (reader.IsEmptyElement)
                        {
                            values.Add(string.Empty);
                            inItem = false;
                        }

                        break;

                    case XmlNodeType.Element when inItem && reader.LocalName == "t":
                        if (!reader.IsEmptyElement)
                        {
                            current.Append(reader.ReadElementContentAsString());
                        }

                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "si":
                        values.Add(current.ToString());
                        inItem = false;
                        break;
                }
            }

            return values;
        }

        private static List<List<string>> ReadSheet(ZipArchiveEntry entry, IReadOnlyList<string> sharedStrings)
        {
            List<List<string>> rows = new();

            using Stream stream = entry.Open();
            using XmlReader reader = XmlReader.Create(stream, SafeSettings());

            List<string> currentRow = new();
            string cellType = string.Empty;
            int columnIndex = 0;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                {
                    currentRow = new List<string>();
                    if (reader.IsEmptyElement)
                    {
                        rows.Add(currentRow);
                    }
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c")
                {
                    cellType = reader.GetAttribute("t") ?? "n";
                    columnIndex = ColumnIndexOf(reader.GetAttribute("r"));

                    if (reader.IsEmptyElement)
                    {
                        PlaceValue(currentRow, columnIndex, string.Empty);
                    }
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "v")
                {
                    string raw = reader.ReadElementContentAsString();
                    PlaceValue(currentRow, columnIndex, Resolve(raw, cellType, sharedStrings));
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && cellType == "inlineStr")
                {
                    PlaceValue(currentRow, columnIndex, reader.ReadElementContentAsString());
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row")
                {
                    rows.Add(currentRow);
                }
            }

            TrimTrailingBlankRows(rows);
            return rows;
        }

        private static string Resolve(string raw, string cellType, IReadOnlyList<string> sharedStrings)
        {
            switch (cellType)
            {
                case "s":
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                           && index >= 0 && index < sharedStrings.Count
                        ? sharedStrings[index]
                        : string.Empty;

                case "b":
                    return raw == "1" ? "TRUE" : "FALSE";

                default:
                    return raw;
            }
        }

        private static void PlaceValue(List<string> row, int columnIndex, string value)
        {
            if (columnIndex <= 0)
            {
                row.Add(value);
                return;
            }

            while (row.Count < columnIndex - 1)
            {
                row.Add(string.Empty);
            }

            if (row.Count == columnIndex - 1)
            {
                row.Add(value);
            }
            else
            {
                row[columnIndex - 1] = value;
            }
        }

        /// <summary>Extracts the one-based column index from an A1-style cell reference.</summary>
        /// <param name="reference">The reference, for example <c>AB12</c>.</param>
        /// <returns>The column index, or zero when the reference is absent or malformed.</returns>
        public static int ColumnIndexOf(string? reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return 0;
            }

            int index = 0;
            foreach (char character in reference!)
            {
                if (character is >= 'A' and <= 'Z')
                {
                    index = (index * 26) + (character - 'A' + 1);
                }
                else if (character is >= 'a' and <= 'z')
                {
                    index = (index * 26) + (character - 'a' + 1);
                }
                else
                {
                    break;
                }
            }

            return index;
        }

        private static void TrimTrailingBlankRows(List<List<string>> rows)
        {
            while (rows.Count > 0 && rows[^1].All(string.IsNullOrWhiteSpace))
            {
                rows.RemoveAt(rows.Count - 1);
            }
        }

        /// <summary>
        /// Builds reader settings that disable document type definitions and external entity
        /// resolution, so that opening an untrusted workbook cannot be turned into a file
        /// disclosure or a denial of service.
        /// </summary>
        /// <returns>The settings.</returns>
        private static XmlReaderSettings SafeSettings() => new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = false,
            CloseInput = false
        };
    }
}
