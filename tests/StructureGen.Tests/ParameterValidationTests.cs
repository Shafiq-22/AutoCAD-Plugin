using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Geometry;
using Xunit;

namespace StructureGen.Tests
{
    public class ParameterValidationTests
    {
        private static readonly ElementGeneratorRegistry Registry = TestData.Registry();

        private static SectionRecord Ipe300Record() => new(
            "en10365-ipe-300", "en-10365", ElementCategory.ISection, "IPE 300",
            TestData.Ipe300, unitWeightKgPerM: 42.2m, verified: true);

        [Fact]
        public void A_standard_profile_used_as_published_is_valid()
        {
            SectionRecord record = Ipe300Record();
            ElementDefinition definition = TestData.Element(ElementCategory.ISection, record.CopyDimensions());

            ValidationReport report = Registry.Require(ElementCategory.ISection).Validate(definition, record);

            Assert.True(report.IsValid, report.ToDisplayString());
        }

        [Fact]
        public void Changing_a_standardised_dimension_outside_custom_mode_is_rejected()
        {
            // This is the guard that stops an IPE 300 quietly becoming something that exists in no
            // catalogue. It is enforced here as well as in the palette, so constructing a definition
            // in code cannot bypass it either.
            SectionRecord record = Ipe300Record();
            Dictionary<string, decimal> altered = record.CopyDimensions();
            altered["tw"] = 12m;

            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, altered), record);

            Assert.False(report.IsValid);
            ValidationMessage message = report.Messages.Single(m => m.Code == "Element.StandardDimensionAltered");
            Assert.Contains("Web thickness", message.Message);
            Assert.Contains("custom mode", message.Message);
        }

        [Fact]
        public void Changing_a_standardised_dimension_in_custom_mode_is_allowed()
        {
            SectionRecord record = Ipe300Record();
            Dictionary<string, decimal> altered = record.CopyDimensions();
            altered["tw"] = 12m;

            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, altered, isCustom: true), record);

            Assert.True(report.IsValid, report.ToDisplayString());
        }

        [Fact]
        public void Changing_only_the_length_never_trips_the_standard_dimension_guard()
        {
            // The property the whole design turns on: length is an instance parameter and moves
            // freely, while the cross-section stays exactly as the standard published it.
            SectionRecord record = Ipe300Record();

            foreach (decimal length in new[] { 1m, 1000m, 6000m, 18000m })
            {
                ValidationReport report = Registry.Require(ElementCategory.ISection).Validate(
                    TestData.Element(ElementCategory.ISection, record.CopyDimensions(), lengthMm: length), record);

                Assert.True(report.IsValid, $"A length of {length} mm was rejected: {report.ToDisplayString()}");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void A_quantity_below_one_is_rejected(int quantity)
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, quantity: quantity));

            Assert.Contains(report.Messages, m => m.Code == "Element.QuantityTooSmall");
        }

        [Fact]
        public void An_absurd_quantity_is_rejected_with_a_suggestion()
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, quantity: 500_000));

            Assert.Contains(report.Messages, m => m.Code == "Element.QuantityTooLarge");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void A_length_of_zero_or_less_is_rejected(int length)
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: length));

            Assert.Contains(report.Messages, m => m.Code == "Element.LengthNotPositive");
        }

        [Fact]
        public void A_missing_length_on_a_linear_member_is_rejected()
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: null));

            Assert.Contains(report.Messages, m => m.Code == "Element.LengthMissing");
        }

        [Fact]
        public void A_length_over_a_kilometre_is_rejected_as_a_probable_unit_mistake()
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: 2_000_000m));

            ValidationMessage message = report.Messages.Single(m => m.Code == "Element.LengthTooLarge");
            Assert.Contains("millimetres", message.Message);
        }

        [Fact]
        public void A_long_but_possible_length_is_a_warning_rather_than_an_error()
        {
            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: 150_000m));

            Assert.True(report.IsValid);
            Assert.Contains(report.Messages, m => m.Code == "Element.LengthImplausible");
        }

        [Fact]
        public void A_missing_required_dimension_is_rejected_and_named()
        {
            Dictionary<string, decimal> incomplete = new(TestData.Ipe300);
            incomplete.Remove("tw");

            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, incomplete));

            Assert.Contains(report.Messages, m => m.Code == "Element.MissingDimension" && m.Message.Contains("tw"));
        }

        [Fact]
        public void A_section_whose_flanges_leave_no_web_is_rejected()
        {
            Dictionary<string, decimal> impossible = new(TestData.Ipe300) { ["tf"] = 160m };

            ValidationReport report = Registry.Require(ElementCategory.ISection)
                .Validate(TestData.Element(ElementCategory.ISection, impossible));

            Assert.Contains(report.Messages, m => m.Code == "ISection.NoWeb");
        }

        [Fact]
        public void A_pipe_whose_wall_leaves_no_bore_is_rejected()
        {
            ValidationReport report = Registry.Require(ElementCategory.Pipe).Validate(
                TestData.Element(ElementCategory.Pipe, new Dictionary<string, decimal> { ["d"] = 100m, ["t"] = 50m }));

            Assert.Contains(report.Messages, m => m.Code == "Pipe.NoBore");
        }

        [Fact]
        public void A_washer_with_no_material_left_is_rejected()
        {
            ValidationReport report = Registry.Require(ElementCategory.Washer).Validate(TestData.Element(
                ElementCategory.Washer,
                new Dictionary<string, decimal> { ["d1"] = 40m, ["d2"] = 30m, ["hw"] = 3m },
                lengthMm: null));

            Assert.Contains(report.Messages, m => m.Code == "Washer.NoMaterial");
        }

        [Fact]
        public void A_bolt_length_inside_the_range_the_standard_publishes_is_accepted()
        {
            ValidationReport report = Registry.Require(ElementCategory.Bolt).Validate(TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal>
                {
                    ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 80m, ["lmin"] = 35m, ["lmax"] = 200m
                },
                lengthMm: null));

            Assert.True(report.IsValid, report.ToDisplayString());
        }

        [Theory]
        [InlineData(20, "Bolt.LengthBelowStandardRange")]
        [InlineData(400, "Bolt.LengthAboveStandardRange")]
        public void A_bolt_length_outside_the_published_range_is_rejected(int length, string expectedCode)
        {
            ValidationReport report = Registry.Require(ElementCategory.Bolt).Validate(TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal>
                {
                    ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = length, ["lmin"] = 35m, ["lmax"] = 200m
                },
                lengthMm: null));

            Assert.Contains(report.Messages, m => m.Code == expectedCode);
        }

        [Fact]
        public void Bolt_length_is_adjustable_rather_than_locked_because_the_standard_publishes_a_range()
        {
            SectionRecord record = new(
                "iso4014-m16", "iso-4014", ElementCategory.Bolt, "M16 hex bolt",
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m });

            ParameterSchema schema = Registry.Require(ElementCategory.Bolt).GetParameterSchema(record);

            Assert.False(schema.IsLocked(DimensionKeys.BoltLength));
            Assert.True(schema.IsLocked(DimensionKeys.ThreadDiameter));
            Assert.True(schema.IsLocked(DimensionKeys.AcrossFlats));
        }

        [Fact]
        public void A_plate_is_cut_to_size_so_its_dimensions_are_adjustable()
        {
            SectionRecord record = new(
                "plate-10", "generic", ElementCategory.Plate, "PL 10", TestData.Plate);

            ParameterSchema schema = Registry.Require(ElementCategory.Plate).GetParameterSchema(record);

            Assert.False(schema.IsLocked(DimensionKeys.PlateWidth));
            Assert.False(schema.IsLocked(DimensionKeys.Thickness));
        }

        [Fact]
        public void A_flat_bar_is_rolled_to_standard_sizes_so_its_dimensions_are_locked()
        {
            SectionRecord record = new(
                "flat-50x8", "generic", ElementCategory.FlatBar, "FL 50x8", TestData.Plate);

            ParameterSchema schema = Registry.Require(ElementCategory.FlatBar).GetParameterSchema(record);

            Assert.True(schema.IsLocked(DimensionKeys.PlateWidth));
            Assert.True(schema.IsLocked(DimensionKeys.Thickness));
        }

        [Fact]
        public void Every_schema_offers_a_quantity_and_marks_it_as_belonging_to_the_occurrence()
        {
            SectionRecord record = Ipe300Record();

            foreach (IElementGenerator generator in Registry.Generators)
            {
                ParameterSchema schema = generator.GetParameterSchema(record);
                ParameterDescriptor? quantity = schema.Find(InstanceParameterKeys.Quantity);

                Assert.NotNull(quantity);
                Assert.Equal(ParameterMutability.Instance, quantity!.Mutability);
            }
        }

        [Fact]
        public void Only_categories_that_have_a_length_offer_one()
        {
            SectionRecord record = Ipe300Record();

            Assert.NotNull(Registry.Require(ElementCategory.ISection)
                .GetParameterSchema(record).Find(InstanceParameterKeys.Length));

            Assert.Null(Registry.Require(ElementCategory.Bolt)
                .GetParameterSchema(record).Find(InstanceParameterKeys.Length));
        }

        [Fact]
        public void The_registry_reports_which_dimensions_a_category_needs()
        {
            IReadOnlyList<string> required = Registry.RequiredDimensions(ElementCategory.ISection);

            Assert.Contains(DimensionKeys.Height, required);
            Assert.Contains(DimensionKeys.WebThickness, required);
            Assert.Empty(Registry.RequiredDimensions("not-a-category"));
        }

        [Fact]
        public void Building_geometry_from_invalid_parameters_fails_without_throwing()
        {
            Result<Core.Geometry.ElementGeometry> result = Registry.Require(ElementCategory.ISection)
                .BuildGeometry(TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: -1m));

            Assert.True(result.IsFailure);
            Assert.Equal("Geometry.InvalidParameters", result.ErrorCode);
        }
    }
}
