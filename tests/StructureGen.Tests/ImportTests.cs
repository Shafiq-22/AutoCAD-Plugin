using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Tabular;
using StructureGen.Data.Import;
using Xunit;

namespace StructureGen.Tests
{
    public class ImportTests
    {
        private static MappingProfile Mapping() => new()
        {
            Name = "test mapping",
            StandardId = "en-10365",
            Category = ElementCategory.ISection,
            DesignationColumn = "Designation",
            UnitWeightColumn = "Mass",
            Dimensions = new Dictionary<string, string>
            {
                ["h"] = "Depth h", ["b"] = "Width b", ["tw"] = "Web tw", ["tf"] = "Flange tf", ["r"] = "Root radius r"
            },
            Material = "s355jr"
        };

        [Fact]
        public void A_plain_table_is_read()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Root radius r,Mass\n" +
                "IPE 300,300,150,7.1,10.7,15,42.2\n", "test.csv");

            Assert.Equal(7, source.Headers.Count);
            Assert.Single(source.Rows);
            Assert.Equal("IPE 300", source.Rows[0][0]);
        }

        [Fact]
        public void A_quoted_field_containing_a_comma_is_kept_whole()
        {
            // Section designations routinely contain commas. Splitting on every comma would corrupt
            // the row and silently shift every column after it.
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Mass\n\"IPE 330, heavy variant\",49.1\n", "test.csv");

            Assert.Equal("IPE 330, heavy variant", source.Rows[0][0]);
            Assert.Equal("49.1", source.Rows[0][1]);
        }

        [Fact]
        public void A_doubled_quote_inside_a_quoted_field_is_one_quote()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Note\n\"L 100\",\"the \"\"long\"\" leg\"\n", "test.csv");

            Assert.Equal("the \"long\" leg", source.Rows[0][1]);
        }

        [Fact]
        public void A_quoted_field_may_span_lines()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Note\nIPE 300,\"first line\nsecond line\"\n", "test.csv");

            Assert.Single(source.Rows);
            Assert.Contains("second line", source.Rows[0][1]);
        }

        [Fact]
        public void Blank_lines_are_skipped()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Mass\nIPE 300,42.2\n\n\nIPE 400,66.3\n", "test.csv");

            Assert.Equal(2, source.Rows.Count);
        }

        [Fact]
        public void Carriage_returns_do_not_end_up_in_the_data()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Mass\r\nIPE 300,42.2\r\n", "test.csv");

            Assert.Equal("42.2", source.Rows[0][1]);
        }

        [Fact]
        public void A_tab_separated_file_is_read_with_the_right_delimiter()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation\tMass\nIPE 300\t42.2\n", "test.tsv", '\t');

            Assert.Equal(2, source.Headers.Count);
            Assert.Equal("IPE 300", source.Rows[0][0]);
        }

        [Fact]
        public void A_mapped_table_becomes_section_records()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Root radius r,Mass\n" +
                "IPE 300,300,150,7.1,10.7,15,42.2\n" +
                "IPE 400,400,180,8.6,13.5,21,66.3\n", "test.csv");

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Equal(2, outcome.Sections.Count);
            Assert.Equal(0, outcome.RowsSkipped);

            SectionRecord first = outcome.Sections[0];
            Assert.Equal("IPE 300", first.Designation);
            Assert.Equal(ElementCategory.ISection, first.CategoryId);
            Assert.Equal("en-10365", first.StandardId);
            Assert.Equal(300m, first.Dimension("h"));
            Assert.Equal(42.2m, first.UnitWeightKgPerM);
            Assert.Equal("s355jr", first.Material);
        }

        [Fact]
        public void Imported_records_are_never_marked_verified_unless_the_profile_says_so()
        {
            // The importer must not assert on the user's behalf that their data has been checked.
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\nIPE 300,300,150,7.1,10.7,42.2\n", "t.csv");

            Assert.False(new TableImporter().Import(source, Mapping()).Sections[0].Verified);

            MappingProfile trusted = Mapping();
            trusted.MarkVerified = true;

            Assert.True(new TableImporter().Import(source, trusted).Sections[0].Verified);
        }

        [Fact]
        public void An_imported_record_records_where_it_came_from()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\nIPE 300,300,150,7.1,10.7,42.2\n", "catalogue.csv");

            SectionRecord record = new TableImporter().Import(source, Mapping()).Sections[0];

            Assert.Contains("catalogue.csv", record.Provenance);
        }

        [Fact]
        public void An_imperial_mapping_converts_on_import()
        {
            MappingProfile profile = Mapping();
            profile.LengthUnit = "in";
            profile.UnitWeightUnit = "lb/ft";

            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\nW12X26,12,6.49,0.23,0.38,26\n", "aisc.csv");

            SectionRecord record = new TableImporter().Import(source, profile).Sections[0];

            Assert.Equal(304.8m, record.Dimension("h"));
            Assert.Equal(38.6923m, decimal.Round(record.UnitWeightKgPerM!.Value, 4));
        }

        [Fact]
        public void A_row_without_a_designation_is_skipped_with_a_reason()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\n,300,150,7.1,10.7,42.2\n", "t.csv");

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Empty(outcome.Sections);
            Assert.Contains(outcome.Report.Messages, m => m.Code == "Import.NoDesignation");
        }

        [Fact]
        public void A_duplicate_designation_is_skipped_rather_than_silently_overwriting()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\n" +
                "IPE 300,300,150,7.1,10.7,42.2\nIPE 300,300,150,7.1,10.7,42.2\n", "t.csv");

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Single(outcome.Sections);
            Assert.Contains(outcome.Report.Messages, m => m.Code == "Import.DuplicateId");
        }

        [Fact]
        public void A_cell_that_is_not_a_number_leaves_the_dimension_unset_and_says_which()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Mass\nIPE 300,three hundred,150,7.1,10.7,42.2\n", "t.csv");

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Null(outcome.Sections[0].Dimension("h"));
            Assert.Contains(outcome.Report.Messages, m => m.Code == "Import.UnparsableNumber");
        }

        [Fact]
        public void A_blank_marker_is_treated_as_absent_rather_than_as_zero()
        {
            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Depth h,Width b,Web tw,Flange tf,Root radius r,Mass\n" +
                "IPE 300,300,150,7.1,10.7,-,42.2\n", "t.csv");

            SectionRecord record = new TableImporter().Import(source, Mapping()).Sections[0];

            Assert.Null(record.Dimension("r"));
            Assert.Equal(300m, record.Dimension("h"));
        }

        [Fact]
        public void A_column_the_mapping_names_but_the_file_lacks_is_reported()
        {
            ITabularSource source = CsvTabularSource.Parse("Designation,Mass\nIPE 300,42.2\n", "t.csv");

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Contains(outcome.Report.Messages, m => m.Code == "Import.MissingColumn");
        }

        [Fact]
        public void A_mapping_with_no_designation_column_fails_before_reading_a_single_row()
        {
            MappingProfile broken = Mapping();
            broken.DesignationColumn = null;

            ImportOutcome outcome = new TableImporter().Import(
                CsvTabularSource.Parse("A,B\n1,2\n", "t.csv"), broken);

            Assert.Contains(outcome.Report.Messages, m => m.Code == "Import.NoDesignationColumn");
        }

        [Fact]
        public void A_per_row_category_column_overrides_the_profile_default()
        {
            MappingProfile profile = Mapping();
            profile.CategoryColumn = "Kind";

            ITabularSource source = CsvTabularSource.Parse(
                "Designation,Kind,Depth h,Width b,Web tw,Flange tf,Mass\n" +
                "HEB 300,h-section,300,300,11,19,117\n", "t.csv");

            Assert.Equal(ElementCategory.HSection, new TableImporter().Import(source, profile).Sections[0].CategoryId);
        }

        [Theory]
        [InlineData("42.2", 42.2)]
        [InlineData("42,2", 42.2)]
        [InlineData("1,234.5", 1234.5)]
        [InlineData("1.234,5", 1234.5)]
        [InlineData("1 234.5", 1234.5)]
        [InlineData("42.2 kg/m", 42.2)]
        [InlineData("  7.1  ", 7.1)]
        [InlineData("-3.5", -3.5)]
        public void Numbers_are_parsed_the_way_published_tables_write_them(string text, double expected)
        {
            Assert.True(TableImporter.TryParseNumber(text, out decimal value), $"'{text}' was not parsed.");
            Assert.Equal((decimal)expected, value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("n/a")]
        [InlineData("not a number at all")]
        [InlineData(null)]
        public void Text_that_is_not_a_number_is_rejected_rather_than_guessed(string? text) =>
            Assert.False(TableImporter.TryParseNumber(text, out _));

        [Fact]
        public void The_shipped_sample_table_imports_cleanly()
        {
            // The sample CSV and the mapping profile that ships beside it are exercised together, so
            // the worked example in the documentation is known to work.
            ITabularSource source = CsvTabularSource.FromFile(
                System.IO.Path.Combine(TestData.DataDirectory, "import-mappings", "sample-i-sections.csv"));

            ImportOutcome outcome = new TableImporter().Import(source, Mapping());

            Assert.Equal(6, outcome.Sections.Count);
            Assert.DoesNotContain(outcome.Report.Messages, m => m.Severity == Severity.Error);
            Assert.Contains(outcome.Sections, s => s.Designation == "IPE 330, heavy variant");
        }
    }
}
