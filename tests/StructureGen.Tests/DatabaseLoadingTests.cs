using System;
using System.IO;
using System.Linq;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Data;
using StructureGen.Data.Json;
using StructureGen.Geometry;
using Xunit;

namespace StructureGen.Tests
{
    public class DatabaseLoadingTests
    {
        private static Result<StructuralDatabase> Parse(string json)
        {
            StructuralDatabase? loaded = null;
            ElementGeneratorRegistry registry = ElementGeneratorRegistry.CreateDefault(id => loaded?.FindSection(id));
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator(registry));

            Result<StructuralDatabase> result = loader.Parse(json, "test.json");

            if (result.IsSuccess)
            {
                loaded = result.Value;
            }

            return result;
        }

        private const string MinimalDatabase = """
        {
          "schemaVersion": "1.0",
          "database": { "id": "t", "name": "Test", "version": "1" },
          "units": { "length": "mm", "unitWeight": "kg/m" },
          "standards": [ { "id": "s1", "name": "Standard One" } ],
          "materials": [ { "id": "m1", "name": "Steel", "grade": "S355", "density": 7850 } ],
          "sections": [
            {
              "id": "s1-ipe300", "standardId": "s1", "category": "i-section", "designation": "IPE 300",
              "material": "m1", "unitWeight": 42.2,
              "dimensions": { "h": 300, "b": 150, "tw": 7.1, "tf": 10.7, "r": 15 },
              "verified": true
            }
          ]
        }
        """;

        [Fact]
        public void A_valid_database_loads()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Single(result.Value.Sections);
            Assert.Equal("IPE 300", result.Value.Sections[0].Designation);
            Assert.Equal(42.2m, result.Value.Sections[0].UnitWeightKgPerM);
        }

        [Fact]
        public void A_missing_file_is_reported_by_path_rather_than_by_exception()
        {
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator());
            string missing = Path.Combine(Path.GetTempPath(), "structuregen-does-not-exist-" + Guid.NewGuid() + ".json");

            Result<StructuralDatabase> result = loader.Load(missing);

            Assert.True(result.IsFailure);
            Assert.Equal("Db.NotFound", result.ErrorCode);
            Assert.Contains(missing, result.ErrorMessage);
        }

        [Fact]
        public void An_empty_path_is_reported_as_unconfigured()
        {
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator());

            Result<StructuralDatabase> result = loader.Load("   ");

            Assert.True(result.IsFailure);
            Assert.Equal("Db.NoPath", result.ErrorCode);
        }

        [Fact]
        public void Malformed_json_is_reported_without_a_stack_trace()
        {
            Result<StructuralDatabase> result = Parse("{ \"schemaVersion\": \"1.0\", ");

            Assert.True(result.IsFailure);
            Assert.Equal("Db.InvalidJson", result.ErrorCode);
            Assert.DoesNotContain("Exception", result.ErrorMessage);
        }

        [Fact]
        public void A_different_major_schema_version_is_refused_rather_than_misread()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"1.0\"", "\"2.0\""));

            Assert.True(result.IsFailure);
            Assert.Equal("Db.UnsupportedSchema", result.ErrorCode);
        }

        [Fact]
        public void A_newer_minor_schema_version_is_accepted_because_minor_revisions_only_add_optional_fields()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"1.7\""));

            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        [Fact]
        public void A_section_missing_its_designation_names_the_record_in_the_error()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"designation\": \"IPE 300\",", string.Empty));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.MissingDesignation");
        }

        [Fact]
        public void A_section_missing_a_dimension_its_generator_needs_is_rejected()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"tw\": 7.1,", string.Empty));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.MissingDimension" && m.Message.Contains("tw"));
        }

        [Fact]
        public void A_duplicate_section_identifier_is_rejected()
        {
            string json = MinimalDatabase.Replace(
                "\"sections\": [",
                """
                "sections": [
                    { "id": "s1-ipe300", "standardId": "s1", "category": "i-section", "designation": "Other",
                      "dimensions": { "h": 200, "b": 100, "tw": 5.6, "tf": 8.5 }, "unitWeight": 22.4 },
                """);

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.DuplicateSectionId");
        }

        [Fact]
        public void A_section_pointing_at_a_standard_that_does_not_exist_is_rejected()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"standardId\": \"s1\"", "\"standardId\": \"nope\""));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.UnknownStandard");
        }

        [Fact]
        public void A_negative_unit_weight_is_rejected()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"unitWeight\": 42.2", "\"unitWeight\": -42.2"));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.InvalidUnitWeight");
        }

        [Fact]
        public void A_section_whose_flanges_fill_its_depth_is_rejected_as_impossible()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"tf\": 10.7", "\"tf\": 160"));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.FlangeTooThick");
        }

        [Fact]
        public void A_section_with_no_mass_and_no_density_is_warned_about_but_still_loads()
        {
            string json = MinimalDatabase
                .Replace("\"unitWeight\": 42.2,", string.Empty)
                .Replace("\"material\": \"m1\",", string.Empty);

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.NoMassSource");
        }

        [Fact]
        public void An_unverified_record_is_reported_so_it_can_be_flagged_downstream()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"verified\": true", "\"verified\": false"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1, result.Value.UnverifiedRecordCount);
            Assert.False(result.Value.AllRecordsVerified);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Unverified");
        }

        [Fact]
        public void A_file_declaring_imperial_units_is_converted_on_load()
        {
            string json = MinimalDatabase
                .Replace("\"length\": \"mm\", \"unitWeight\": \"kg/m\"", "\"length\": \"in\", \"unitWeight\": \"lb/ft\"")
                .Replace("\"h\": 300", "\"h\": 12")
                .Replace("\"b\": 150", "\"b\": 6")
                .Replace("\"tw\": 7.1", "\"tw\": 0.25")
                .Replace("\"tf\": 10.7", "\"tf\": 0.4")
                .Replace("\"r\": 15", "\"r\": 0.5")
                .Replace("\"unitWeight\": 42.2", "\"unitWeight\": 26");

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsSuccess, result.ErrorMessage);

            SectionRecord record = result.Value.Sections[0];

            // Twelve inches is exactly 304.8 mm.
            Assert.Equal(304.8m, record.Dimension("h"));

            // 26 lb/ft is 26 * 0.45359237 / 0.3048 kg/m.
            Assert.Equal(38.6923m, Math.Round(record.UnitWeightKgPerM!.Value, 4));
        }

        [Fact]
        public void A_tabulated_area_converts_by_the_square_of_the_length_factor()
        {
            string json = MinimalDatabase
                .Replace("\"length\": \"mm\"", "\"length\": \"cm\"")
                .Replace("\"h\": 300", "\"h\": 30, \"A\": 53.8")
                .Replace("\"b\": 150", "\"b\": 15")
                .Replace("\"tw\": 7.1", "\"tw\": 0.71")
                .Replace("\"tf\": 10.7", "\"tf\": 1.07")
                .Replace("\"r\": 15", "\"r\": 1.5");

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsSuccess, result.ErrorMessage);

            // 53.8 cm2 is 5380 mm2: the factor is ten squared, not ten.
            Assert.Equal(5380m, result.Value.Sections[0].Dimension("A"));
        }

        [Fact]
        public void An_unknown_unit_token_falls_back_to_the_default_and_says_so()
        {
            Result<StructuralDatabase> result = Parse(MinimalDatabase.Replace("\"length\": \"mm\"", "\"length\": \"cubits\""));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.UnknownUnit");
            Assert.Equal(300m, result.Value.Sections[0].Dimension("h"));
        }

        [Fact]
        public void An_explicit_outline_is_read_and_scaled()
        {
            string json = MinimalDatabase.Replace(
                "\"verified\": true",
                """
                "verified": true,
                "outline": [ { "x": 0, "y": 0 }, { "x": 100, "y": 0, "bulge": 0.5 }, { "x": 100, "y": 50 }, { "x": 0, "y": 50 } ]
                """);

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsSuccess, result.ErrorMessage);

            SectionRecord record = result.Value.Sections[0];
            Assert.True(record.HasOutline);
            Assert.Equal(4, record.Outline!.Count);
            Assert.Equal(0.5d, record.Outline[1].Bulge);
        }

        [Fact]
        public void An_outline_of_fewer_than_three_points_is_rejected()
        {
            string json = MinimalDatabase.Replace(
                "\"verified\": true",
                "\"verified\": true, \"outline\": [ { \"x\": 0, \"y\": 0 }, { \"x\": 1, \"y\": 1 } ]");

            Result<StructuralDatabase> result = Parse(json);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Report!.Messages, m => m.Code == "Db.Section.ShortOutline");
        }

        [Fact]
        public void The_shipped_demonstration_database_loads_and_is_marked_unverified()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();

            Assert.NotEmpty(database.Sections);
            Assert.False(database.Metadata.Verified);
            Assert.Equal(database.Sections.Count, database.UnverifiedRecordCount);
            Assert.Contains("DEMONSTRATION", database.Metadata.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Every_record_in_the_shipped_database_names_its_provenance()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();

            Assert.All(database.Sections, section =>
                Assert.False(string.IsNullOrWhiteSpace(section.Provenance),
                    $"'{section.Designation}' does not say where its numbers came from."));
        }

        [Fact]
        public void The_shipped_imperial_database_picks_up_the_shared_standards_file()
        {
            StructuralDatabase? loaded = null;
            ElementGeneratorRegistry registry = ElementGeneratorRegistry.CreateDefault(id => loaded?.FindSection(id));
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator(registry));

            Result<StructuralDatabase> result = loader.Load(TestData.ImperialDatabasePath);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            loaded = result.Value;

            Assert.NotEmpty(loaded.Standards);
            Assert.NotNull(loaded.FindStandard("aisc-360"));

            // The density came from the shared file, which declares kilograms per cubic metre, and
            // must not have been reinterpreted using this file's pounds per cubic inch.
            Assert.Equal(7850m, loaded.Materials.First(m => m.Id == "s355jr").DensityKgPerCubicMetre);
        }

        [Fact]
        public void Indexes_answer_the_questions_the_palette_asks()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();

            Assert.Contains(ElementCategory.ISection, database.Categories);
            Assert.NotEmpty(database.StandardsInCategory(ElementCategory.ISection));
            Assert.NotEmpty(database.SectionsInCategory(ElementCategory.ISection, "en-10365"));
            Assert.Null(database.FindSection("no-such-record"));
        }
    }
}
