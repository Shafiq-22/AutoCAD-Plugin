using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>
    /// Builds and writes a real <c>.xlsx</c> workbook using nothing but the framework's own ZIP
    /// support.
    /// </summary>
    /// <remarks>
    /// StructureGen writes SpreadsheetML itself rather than taking a spreadsheet library. The
    /// plugin is loaded into the AutoCAD process alongside whatever other add-ins the user has
    /// installed, and every third-party assembly it drags in is a chance of an assembly-resolution
    /// conflict with a different version of the same assembly loaded by something else. A workbook
    /// of a few thousand schedule lines needs a fraction of the format, so the whole exporter costs
    /// less than the risk of the dependency.
    ///
    /// The output is a genuine Office Open XML package — not CSV renamed, and not an HTML table —
    /// and opens in Excel, LibreOffice and Google Sheets without a repair prompt.
    /// </remarks>
    public sealed class XlsxWorkbook
    {
        private readonly List<XlsxWorksheet> _sheets = new();

        /// <summary>Gets or sets the document title recorded in the package properties.</summary>
        public string Title { get; set; } = "StructureGen export";

        /// <summary>Gets or sets the author recorded in the package properties.</summary>
        public string Creator { get; set; } = "StructureGen";

        /// <summary>Gets or sets the application name recorded in the package properties.</summary>
        public string Application { get; set; } = "StructureGen";

        /// <summary>Gets the worksheets, in tab order.</summary>
        public IReadOnlyList<XlsxWorksheet> Sheets => _sheets;

        /// <summary>Adds a worksheet.</summary>
        /// <param name="name">
        /// The tab name. It is sanitised to satisfy the spreadsheet format's rules: at most
        /// 31 characters, and none of <c>: \ / ? * [ ]</c>.
        /// </param>
        /// <returns>The new worksheet.</returns>
        /// <exception cref="ArgumentException">A sheet with the same sanitised name already exists.</exception>
        public XlsxWorksheet AddSheet(string name)
        {
            string sanitised = SanitiseSheetName(name);

            if (_sheets.Any(s => string.Equals(s.Name, sanitised, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"A worksheet named '{sanitised}' already exists in this workbook.", nameof(name));
            }

            XlsxWorksheet sheet = new(sanitised);
            _sheets.Add(sheet);
            return sheet;
        }

        /// <summary>Writes the workbook to a file.</summary>
        /// <param name="path">The path to write to. Any existing file is replaced.</param>
        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
            Save(stream);
        }

        /// <summary>Writes the workbook to a stream.</summary>
        /// <param name="stream">The stream to write to. It is left open.</param>
        public void Save(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (_sheets.Count == 0)
            {
                throw new InvalidOperationException("A workbook must contain at least one worksheet.");
            }

            XlsxSharedStrings sharedStrings = new();

            // Worksheets are rendered first because doing so populates the shared string table that
            // the sharedStrings part is then written from.
            List<string> sheetXml = _sheets.Select(sheet => sheet.ToXml(sharedStrings)).ToList();

            using ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true);

            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildPackageRelationships());
            WriteEntry(archive, "docProps/core.xml", BuildCoreProperties());
            WriteEntry(archive, "docProps/app.xml", BuildApplicationProperties());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", XlsxStyleTable.Build());
            WriteEntry(archive, "xl/sharedStrings.xml", sharedStrings.ToXml());

            for (int index = 0; index < sheetXml.Count; index++)
            {
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", sheetXml[index]);
            }
        }

        /// <summary>
        /// Reduces a proposed tab name to one the spreadsheet format accepts.
        /// </summary>
        /// <param name="name">The proposed name.</param>
        /// <returns>The sanitised name.</returns>
        public static string SanitiseSheetName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Sheet";
            }

            StringBuilder builder = new(name!.Length);
            foreach (char character in name)
            {
                builder.Append(character is ':' or '\\' or '/' or '?' or '*' or '[' or ']' ? ' ' : character);
            }

            string trimmed = builder.ToString().Trim().Trim('\'');
            if (trimmed.Length == 0)
            {
                return "Sheet";
            }

            return trimmed.Length <= 31 ? trimmed : trimmed.Substring(0, 31);
        }

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using Stream entryStream = entry.Open();

            // No byte-order mark: the parts declare UTF-8 in their XML declaration, and a mark in
            // the middle of a package part upsets some readers.
            using StreamWriter writer = new(entryStream, new UTF8Encoding(false));
            writer.Write(content);
        }

        private string BuildContentTypes()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");

            for (int index = 0; index < _sheets.Count; index++)
            {
                builder.Append("<Override PartName=\"/xl/worksheets/sheet").Append(index + 1)
                       .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }

            builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            builder.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            builder.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
            builder.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            builder.Append("</Types>");
            return builder.ToString();
        }

        private static string BuildPackageRelationships()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<Relationships xmlns=\"").Append(XlsxXml.PackageRelationshipNamespace).Append("\">");
            builder.Append("<Relationship Id=\"rId1\" Type=\"").Append(XlsxXml.DocumentRelationshipNamespace)
                   .Append("/officeDocument\" Target=\"xl/workbook.xml\"/>");
            builder.Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>");
            builder.Append("<Relationship Id=\"rId3\" Type=\"").Append(XlsxXml.DocumentRelationshipNamespace)
                   .Append("/extended-properties\" Target=\"docProps/app.xml\"/>");
            builder.Append("</Relationships>");
            return builder.ToString();
        }

        private string BuildWorkbook()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<workbook xmlns=\"").Append(XlsxXml.SpreadsheetNamespace)
                   .Append("\" xmlns:r=\"").Append(XlsxXml.DocumentRelationshipNamespace).Append("\">");
            builder.Append("<bookViews><workbookView/></bookViews>");
            builder.Append("<sheets>");

            for (int index = 0; index < _sheets.Count; index++)
            {
                builder.Append("<sheet name=\"").Append(XlsxXml.Escape(_sheets[index].Name))
                       .Append("\" sheetId=\"").Append(index + 1)
                       .Append("\" r:id=\"rId").Append(index + 1).Append("\"/>");
            }

            builder.Append("</sheets>");

            // A defined name of _xlnm._FilterDatabase is what makes an auto-filter survive a save
            // and reload; without it some readers drop the drop-downs.
            List<string> definedNames = new();
            for (int index = 0; index < _sheets.Count; index++)
            {
                if (_sheets[index].AutoFilterRange is not { } filter)
                {
                    continue;
                }

                string reference = "'" + _sheets[index].Name.Replace("'", "''", StringComparison.Ordinal) + "'!" +
                                   AbsoluteRange(filter.FirstRow, filter.FirstColumn, filter.LastRow, filter.LastColumn);

                definedNames.Add(
                    "<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"" +
                    index.ToString(CultureInfo.InvariantCulture) + "\" hidden=\"1\">" +
                    XlsxXml.Escape(reference) + "</definedName>");
            }

            if (definedNames.Count > 0)
            {
                builder.Append("<definedNames>");
                foreach (string definedName in definedNames)
                {
                    builder.Append(definedName);
                }

                builder.Append("</definedNames>");
            }

            builder.Append("</workbook>");
            return builder.ToString();
        }

        private string BuildWorkbookRelationships()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<Relationships xmlns=\"").Append(XlsxXml.PackageRelationshipNamespace).Append("\">");

            for (int index = 0; index < _sheets.Count; index++)
            {
                builder.Append("<Relationship Id=\"rId").Append(index + 1)
                       .Append("\" Type=\"").Append(XlsxXml.DocumentRelationshipNamespace)
                       .Append("/worksheet\" Target=\"worksheets/sheet").Append(index + 1).Append(".xml\"/>");
            }

            builder.Append("<Relationship Id=\"rId").Append(_sheets.Count + 1)
                   .Append("\" Type=\"").Append(XlsxXml.DocumentRelationshipNamespace)
                   .Append("/styles\" Target=\"styles.xml\"/>");
            builder.Append("<Relationship Id=\"rId").Append(_sheets.Count + 2)
                   .Append("\" Type=\"").Append(XlsxXml.DocumentRelationshipNamespace)
                   .Append("/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            builder.Append("</Relationships>");
            return builder.ToString();
        }

        private string BuildCoreProperties()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<cp:coreProperties ")
                   .Append("xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" ")
                   .Append("xmlns:dc=\"http://purl.org/dc/elements/1.1/\" ")
                   .Append("xmlns:dcterms=\"http://purl.org/dc/terms/\" ")
                   .Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            builder.Append("<dc:title>").Append(XlsxXml.Escape(Title)).Append("</dc:title>");
            builder.Append("<dc:creator>").Append(XlsxXml.Escape(Creator)).Append("</dc:creator>");
            builder.Append("<cp:lastModifiedBy>").Append(XlsxXml.Escape(Creator)).Append("</cp:lastModifiedBy>");
            builder.Append("<dcterms:created xsi:type=\"dcterms:W3CDTF\">").Append(timestamp).Append("</dcterms:created>");
            builder.Append("<dcterms:modified xsi:type=\"dcterms:W3CDTF\">").Append(timestamp).Append("</dcterms:modified>");
            builder.Append("</cp:coreProperties>");
            return builder.ToString();
        }

        private string BuildApplicationProperties()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" ")
                   .Append("xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">");
            builder.Append("<Application>").Append(XlsxXml.Escape(Application)).Append("</Application>");
            builder.Append("</Properties>");
            return builder.ToString();
        }

        private static string AbsoluteRange(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
            "$" + XlsxXml.ColumnName(firstColumn) + "$" + firstRow.ToString(CultureInfo.InvariantCulture) +
            ":$" + XlsxXml.ColumnName(lastColumn) + "$" + lastRow.ToString(CultureInfo.InvariantCulture);
    }
}
