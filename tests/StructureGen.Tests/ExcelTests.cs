using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Tabular;
using StructureGen.Excel;
using StructureGen.Excel.Xlsx;
using StructureGen.Services;
using Xunit;

namespace StructureGen.Tests
{
    public class ExcelTests : IDisposable
    {
        private static readonly MassCalculator Calculator = new(TestData.Registry());
        private readonly string _directory;

        public ExcelTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "structuregen-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // Cleaning up a temporary folder is best effort.
            }

            GC.SuppressFinalize(this);
        }

        private static BomDocument SampleBom(bool includeUnweighable = false)
        {
            List<BomEntry> entries = new();

            foreach (int _ in Enumerable.Range(0, 4))
            {
                ElementDefinition beam = TestData.Element(
                    ElementCategory.ISection, TestData.Ipe300, 6000m, 1, 42.2m, designation: "IPE 300");
                entries.Add(new BomEntry(beam, Calculator.Compute(beam), "2A" + entries.Count, "SG-i-section", "Model"));
            }

            ElementDefinition bolts = TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                null, 120, pieceMass: 0.135m, designation: "M16 hex bolt");
            entries.Add(new BomEntry(bolts, Calculator.Compute(bolts), "3B1", "SG-bolt", "Model"));

            if (includeUnweighable)
            {
                ElementDefinition plate = TestData.Element(
                    ElementCategory.Plate, TestData.Plate, 1000m, 2, designation: "PL 10", density: null);
                entries.Add(new BomEntry(plate, Calculator.Compute(plate), "4C1", "SG-plate", "Model"));
            }

            return new BomBuilder().Build(
                entries,
                new BomOptions { Title = "BILL OF MATERIALS", ProjectName = "Test project" },
                "Demo v0.1.0",
                "Model space");
        }

        private string Export(BomDocument bom)
        {
            string path = Path.Combine(_directory, "bom.xlsx");
            Result<string> result = new BomWorkbookBuilder(NullLogger.Instance).Export(bom, path);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(File.Exists(path));
            return path;
        }

        [Fact]
        public void The_export_is_a_real_office_open_xml_package()
        {
            // Not a CSV with the wrong extension, and not an HTML table: an actual package with the
            // parts a spreadsheet application requires.
            using ZipArchive archive = ZipFile.OpenRead(Export(SampleBom()));

            string[] names = archive.Entries.Select(e => e.FullName).ToArray();

            Assert.Contains("[Content_Types].xml", names);
            Assert.Contains("_rels/.rels", names);
            Assert.Contains("xl/workbook.xml", names);
            Assert.Contains("xl/_rels/workbook.xml.rels", names);
            Assert.Contains("xl/styles.xml", names);
            Assert.Contains("xl/sharedStrings.xml", names);
            Assert.Contains("xl/worksheets/sheet1.xml", names);
            Assert.Contains("docProps/core.xml", names);
        }

        [Fact]
        public void Every_part_is_well_formed_xml()
        {
            using ZipArchive archive = ZipFile.OpenRead(Export(SampleBom(includeUnweighable: true)));

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".xml", StringComparison.Ordinal)
                    && !entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
                {
                    continue;
                }

                using Stream stream = entry.Open();

                // Loading throws on malformed XML, which is exactly the failure that makes a
                // spreadsheet application offer to repair a file instead of opening it.
                XDocument document = XDocument.Load(stream);
                Assert.NotNull(document.Root);
            }
        }

        [Fact]
        public void The_workbook_carries_the_four_sheets_the_schedule_needs()
        {
            XDocument workbook = Part(Export(SampleBom()), "xl/workbook.xml");

            string[] sheets = workbook.Descendants()
                .Where(e => e.Name.LocalName == "sheet")
                .Select(e => e.Attribute("name")!.Value)
                .ToArray();

            Assert.Equal(new[] { "BOM", "Summary", "Element Details", "Data Sources" }, sheets);
        }

        [Fact]
        public void The_schedule_sheet_freezes_its_header_and_filters_its_rows()
        {
            XDocument sheet = Part(Export(SampleBom()), "xl/worksheets/sheet1.xml");

            XElement pane = sheet.Descendants().Single(e => e.Name.LocalName == "pane");
            Assert.Equal("frozen", pane.Attribute("state")!.Value);
            Assert.NotNull(pane.Attribute("ySplit"));

            Assert.Contains(sheet.Descendants(), e => e.Name.LocalName == "autoFilter");
        }

        [Fact]
        public void An_auto_filter_is_backed_by_the_defined_name_that_makes_it_persist()
        {
            XDocument workbook = Part(Export(SampleBom()), "xl/workbook.xml");

            Assert.Contains(
                workbook.Descendants().Where(e => e.Name.LocalName == "definedName"),
                e => e.Attribute("name")?.Value == "_xlnm._FilterDatabase");
        }

        [Fact]
        public void Columns_are_given_explicit_widths()
        {
            XDocument sheet = Part(Export(SampleBom()), "xl/worksheets/sheet1.xml");

            List<XElement> columns = sheet.Descendants().Where(e => e.Name.LocalName == "col").ToList();

            Assert.NotEmpty(columns);
            Assert.All(columns, column => Assert.NotNull(column.Attribute("width")));
        }

        [Fact]
        public void The_title_row_is_merged_across_the_schedule()
        {
            XDocument sheet = Part(Export(SampleBom()), "xl/worksheets/sheet1.xml");

            Assert.Contains(sheet.Descendants(), e => e.Name.LocalName == "mergeCell");
        }

        [Fact]
        public void Numbers_are_written_as_numbers_rather_than_as_text()
        {
            // A weight stored as text cannot be summed, which is the first thing anyone does with a
            // schedule after opening it.
            XDocument sheet = Part(Export(SampleBom()), "xl/worksheets/sheet1.xml");

            List<XElement> cells = sheet.Descendants().Where(e => e.Name.LocalName == "c").ToList();

            Assert.Contains(cells, c => c.Attribute("t") is null && c.Descendants().Any(v => v.Name.LocalName == "v"));
        }

        [Fact]
        public void The_shared_string_table_holds_the_designations()
        {
            XDocument strings = Part(Export(SampleBom()), "xl/sharedStrings.xml");

            List<string> values = strings.Descendants()
                .Where(e => e.Name.LocalName == "t")
                .Select(e => e.Value)
                .ToList();

            Assert.Contains("IPE 300", values);
            Assert.Contains("M16 hex bolt", values);
            Assert.Contains("BILL OF MATERIALS", values);
        }

        [Fact]
        public void An_unknown_weight_is_exported_as_a_dash_and_never_as_a_zero()
        {
            XDocument strings = Part(Export(SampleBom(includeUnweighable: true)), "xl/sharedStrings.xml");

            List<string> values = strings.Descendants()
                .Where(e => e.Name.LocalName == "t")
                .Select(e => e.Value)
                .ToList();

            Assert.Contains("-", values);
            Assert.Contains(values, v => v.Contains("EXCLUDED", StringComparison.Ordinal));
        }

        [Fact]
        public void The_workbook_records_where_its_numbers_came_from_and_carries_the_disclaimer()
        {
            XDocument strings = Part(Export(SampleBom()), "xl/sharedStrings.xml");

            string all = string.Join("\n", strings.Descendants()
                .Where(e => e.Name.LocalName == "t")
                .Select(e => e.Value));

            Assert.Contains("Demo v0.1.0", all, StringComparison.Ordinal);
            Assert.Contains("no structural engineering design", all, StringComparison.Ordinal);
            Assert.Contains("Model space", all, StringComparison.Ordinal);
        }

        [Fact]
        public void Text_containing_markup_characters_is_escaped_rather_than_breaking_the_file()
        {
            ElementDefinition awkward = TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, 6000m, 1, 42.2m,
                designation: "IPE 300 <\"special\" & 'odd'>");

            BomDocument bom = new BomBuilder().Build(
                new[] { new BomEntry(awkward, Calculator.Compute(awkward), "1A", "L") }, new BomOptions());

            XDocument strings = Part(Export(bom), "xl/sharedStrings.xml");

            Assert.Contains(
                strings.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value),
                v => v.Contains("<\"special\" & 'odd'>", StringComparison.Ordinal));
        }

        [Fact]
        public void A_workbook_round_trips_through_the_reader()
        {
            XlsxWorkbook workbook = new();
            XlsxWorksheet sheet = workbook.AddSheet("Sections");

            sheet.AddRow().Text("Designation").Text("Depth").Text("Mass");
            sheet.AddRow().Text("IPE 300").Number(300m, XlsxStyle.Number1).Number(42.2m, XlsxStyle.Number2);
            sheet.AddRow().Text("IPE 400").Number(400m, XlsxStyle.Number1).Number(66.3m, XlsxStyle.Number2);

            string path = Path.Combine(_directory, "round-trip.xlsx");
            workbook.Save(path);

            ITabularSource read = XlsxReader.Read(path);

            Assert.Equal(new[] { "Designation", "Depth", "Mass" }, read.Headers);
            Assert.Equal(2, read.Rows.Count);
            Assert.Equal("IPE 300", read.Rows[0][0]);
            Assert.Equal("42.2", read.Rows[0][2]);
        }

        [Fact]
        public void The_reader_keeps_column_positions_when_a_cell_is_blank()
        {
            // A gap must not shift the columns to its right, or an importer would silently read the
            // wrong value into the wrong field.
            XlsxWorkbook workbook = new();
            XlsxWorksheet sheet = workbook.AddSheet("Gaps");

            sheet.AddRow().Text("A").Text("B").Text("C");
            sheet.AddRow().Text("one").Skip().Text("three");

            string path = Path.Combine(_directory, "gaps.xlsx");
            workbook.Save(path);

            ITabularSource read = XlsxReader.Read(path);

            Assert.Equal(3, read.Rows[0].Count);
            Assert.Equal("one", read.Rows[0][0]);
            Assert.Equal(string.Empty, read.Rows[0][1]);
            Assert.Equal("three", read.Rows[0][2]);
        }

        [Fact]
        public void The_reader_lists_and_selects_worksheets_by_name()
        {
            XlsxWorkbook workbook = new();
            workbook.AddSheet("First").AddRow().Text("a");
            workbook.AddSheet("Second").AddRow().Text("b");

            string path = Path.Combine(_directory, "sheets.xlsx");
            workbook.Save(path);

            Assert.Equal(new[] { "First", "Second" }, XlsxReader.SheetNames(path));
            Assert.Equal("b", XlsxReader.Read(path, "Second").Headers[0]);
        }

        [Fact]
        public void Asking_for_a_worksheet_that_does_not_exist_says_which_ones_do()
        {
            XlsxWorkbook workbook = new();
            workbook.AddSheet("Only").AddRow().Text("a");

            string path = Path.Combine(_directory, "one-sheet.xlsx");
            workbook.Save(path);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => XlsxReader.Read(path, "Missing"));

            Assert.Contains("Only", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Exporting_over_a_locked_file_reports_a_message_a_user_can_act_on()
        {
            string path = Path.Combine(_directory, "locked.xlsx");

            using FileStream holder = new(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Result<string> result = new BomWorkbookBuilder(NullLogger.Instance).Export(SampleBom(), path);

            Assert.True(result.IsFailure);
            Assert.Contains("open in Excel", result.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void A_workbook_with_no_sheets_is_refused_rather_than_written_broken() =>
            Assert.Throws<InvalidOperationException>(
                () => new XlsxWorkbook().Save(Path.Combine(_directory, "empty.xlsx")));

        [Fact]
        public void Two_sheets_cannot_share_a_name()
        {
            XlsxWorkbook workbook = new();
            workbook.AddSheet("BOM");

            Assert.Throws<ArgumentException>(() => workbook.AddSheet("bom"));
        }

        [Theory]
        [InlineData("A perfectly ordinary name", "A perfectly ordinary name")]
        [InlineData("Bad: name / with [chars]", "Bad  name   with  chars")]
        [InlineData("", "Sheet")]
        public void Sheet_names_are_reduced_to_what_the_format_accepts(string proposed, string expected) =>
            Assert.Equal(expected, XlsxWorkbook.SanitiseSheetName(proposed));

        [Fact]
        public void A_very_long_sheet_name_is_truncated_to_the_format_limit() =>
            Assert.Equal(31, XlsxWorkbook.SanitiseSheetName(new string('x', 60)).Length);

        [Theory]
        [InlineData("A", 1)]
        [InlineData("Z", 26)]
        [InlineData("AA", 27)]
        [InlineData("AB", 28)]
        [InlineData("ZZ", 702)]
        [InlineData("AAA", 703)]
        public void Column_references_resolve_to_the_right_index(string reference, int expected) =>
            Assert.Equal(expected, XlsxReader.ColumnIndexOf(reference + "12"));

        [Fact]
        public void Control_characters_are_stripped_so_the_file_still_opens()
        {
            // A designation copied out of a PDF can carry a control character that XML forbids
            // outright. Leaving one in produces a file a spreadsheet application refuses to open,
            // offering only to "repair" it.
            XlsxWorkbook workbook = new();
            workbook.AddSheet("Test").AddRow().Text("before\u0001after");

            string path = Path.Combine(_directory, "control.xlsx");
            workbook.Save(path);

            Assert.Equal("beforeafter", XlsxReader.Read(path, "Test").Headers[0]);
        }

        private static XDocument Part(string path, string entryName)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            ZipArchiveEntry entry = archive.GetEntry(entryName)
                                    ?? throw new InvalidOperationException($"The package has no '{entryName}' part.");

            using Stream stream = entry.Open();
            return XDocument.Load(stream);
        }
    }
}
