using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Services;
using Xunit;

namespace StructureGen.Tests
{
    public class MetadataTests
    {
        private static ElementDefinition Sample(Guid? id = null) => new(
            id ?? Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ElementCategory.ISection,
            "en-10365",
            "en10365-ipe-300",
            "IPE 300",
            TestData.Ipe300,
            quantity: 3,
            lengthMm: 6250.5m,
            detailLevel: DetailLevel.Detailed,
            representation: RepresentationMode.Both,
            placements: new[] { new Placement(new Vec3(100d, 200d, 300d), new Vec3(0d, 0d, 1d), 45d, 50d) },
            material: "Structural steel",
            grade: "S355",
            unitWeightKgPerM: 42.2m,
            densityKgPerCubicMetre: 7850m,
            layer: "SG-i-section",
            notes: "Column line B, quoted \"as drawn\"",
            drawingReference: "S-101",
            isCustom: true,
            sourceDataVerified: true,
            createdUtc: new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));

        [Fact]
        public void An_element_survives_a_round_trip_through_the_stored_payload()
        {
            // This is what makes a generated object editable months later: everything needed to
            // rebuild it travels with it, so nothing depends on the session that created it.
            ElementDefinition original = Sample();

            string payload = ElementMetadataCodec.Encode(original, "Demo v1.0", "1.2.3");
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(payload);

            Assert.True(decoded.IsSuccess, decoded.ErrorMessage);

            ElementDefinition restored = decoded.Value.Definition;

            Assert.Equal(original.ElementId, restored.ElementId);
            Assert.Equal(original.CategoryId, restored.CategoryId);
            Assert.Equal(original.StandardId, restored.StandardId);
            Assert.Equal(original.SectionRecordId, restored.SectionRecordId);
            Assert.Equal(original.Designation, restored.Designation);
            Assert.Equal(original.Quantity, restored.Quantity);
            Assert.Equal(original.LengthMm, restored.LengthMm);
            Assert.Equal(original.DetailLevel, restored.DetailLevel);
            Assert.Equal(original.Representation, restored.Representation);
            Assert.Equal(original.Material, restored.Material);
            Assert.Equal(original.Grade, restored.Grade);
            Assert.Equal(original.UnitWeightKgPerM, restored.UnitWeightKgPerM);
            Assert.Equal(original.DensityKgPerCubicMetre, restored.DensityKgPerCubicMetre);
            Assert.Equal(original.Layer, restored.Layer);
            Assert.Equal(original.Notes, restored.Notes);
            Assert.Equal(original.DrawingReference, restored.DrawingReference);
            Assert.Equal(original.IsCustom, restored.IsCustom);
            Assert.Equal(original.SourceDataVerified, restored.SourceDataVerified);
            Assert.Equal(original.CreatedUtc, restored.CreatedUtc);
            Assert.Equal("Demo v1.0", decoded.Value.DatabaseStamp);
            Assert.Equal("1.2.3", decoded.Value.PluginVersion);
        }

        [Fact]
        public void Every_dimension_survives_the_round_trip_to_the_last_decimal()
        {
            ElementDefinition original = Sample();
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(
                ElementMetadataCodec.Encode(original, null, null));

            foreach (KeyValuePair<string, decimal> pair in original.Dimensions)
            {
                Assert.Equal(pair.Value, decoded.Value.Definition.Dimension(pair.Key));
            }
        }

        [Fact]
        public void The_placement_survives_the_round_trip()
        {
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(
                ElementMetadataCodec.Encode(Sample(), null, null));

            Placement placement = decoded.Value.Definition.Placements.Single();

            Assert.True(placement.Origin.IsEqualTo(new Vec3(100d, 200d, 300d), 1e-9));
            Assert.True(placement.AxisDirection.IsEqualTo(Vec3.UnitZ, 1e-9));
            Assert.Equal(45d, placement.RotationDegrees);
            Assert.Equal(50d, placement.Elevation);
        }

        [Fact]
        public void A_custom_outline_travels_with_the_element()
        {
            // A fabricated section is described entirely by its outline. If the outline stayed only
            // in the database, the drawing would stop regenerating the moment the data file changed.
            ProfileVertex[] outline =
            {
                new(0d, 0d), new(100d, 0d, 0.5d), new(100d, 50d), new(0d, 50d)
            };

            ElementDefinition original = TestData.Element(
                ElementCategory.Custom, new Dictionary<string, decimal>(), outline: outline);

            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(
                ElementMetadataCodec.Encode(original, null, null));

            Assert.True(decoded.Value.Definition.HasOutline);
            Assert.Equal(4, decoded.Value.Definition.Outline!.Count);
            Assert.Equal(0.5d, decoded.Value.Definition.Outline[1].Bulge);
            Assert.Equal(100d, decoded.Value.Definition.Outline[1].Point.X);
        }

        [Fact]
        public void Text_containing_quotes_and_punctuation_survives()
        {
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(
                ElementMetadataCodec.Encode(Sample(), null, null));

            Assert.Equal("Column line B, quoted \"as drawn\"", decoded.Value.Definition.Notes);
        }

        [Fact]
        public void An_empty_payload_is_reported_as_not_a_StructureGen_object()
        {
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode("   ");

            Assert.True(decoded.IsFailure);
            Assert.Equal("Metadata.Empty", decoded.ErrorCode);
        }

        [Fact]
        public void A_corrupt_payload_is_reported_without_a_stack_trace()
        {
            Result<DecodedElement> decoded = ElementMetadataCodec.Decode("{ not json");

            Assert.True(decoded.IsFailure);
            Assert.Equal("Metadata.Corrupt", decoded.ErrorCode);
            Assert.DoesNotContain("Exception", decoded.ErrorMessage);
        }

        [Fact]
        public void A_payload_from_a_newer_build_is_refused_with_an_explanation()
        {
            string payload = ElementMetadataCodec.Encode(Sample(), null, null)
                .Replace("\"v\":1", "\"v\":99");

            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(payload);

            Assert.True(decoded.IsFailure);
            Assert.Equal("Metadata.NewerVersion", decoded.ErrorCode);
            Assert.Contains("Update the plugin", decoded.ErrorMessage);
        }

        [Fact]
        public void An_unknown_field_from_a_newer_build_is_ignored_rather_than_rejected()
        {
            // Forward tolerance: a drawing made by a later version must still open here, degraded
            // rather than broken.
            string encoded = ElementMetadataCodec.Encode(Sample(), null, null);
            string payload = "{\"somethingNew\":\"value\"," + encoded.Substring(1);

            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(payload);

            Assert.True(decoded.IsSuccess, decoded.ErrorMessage);
            Assert.Equal("IPE 300", decoded.Value.Definition.Designation);
        }

        [Fact]
        public void A_payload_missing_its_category_is_refused_rather_than_half_read()
        {
            string payload = ElementMetadataCodec.Encode(Sample(), null, null)
                .Replace("\"category\":\"i-section\",", string.Empty);

            Result<DecodedElement> decoded = ElementMetadataCodec.Decode(payload);

            Assert.True(decoded.IsFailure);
            Assert.Equal("Metadata.Incomplete", decoded.ErrorCode);
        }

        [Fact]
        public void A_payload_is_chunked_and_reassembled_exactly()
        {
            string payload = ElementMetadataCodec.Encode(Sample(), "A database with quite a long name", "1.0.0");

            IReadOnlyList<string> chunks = ElementMetadataCodec.Chunk(payload, 40);

            Assert.True(chunks.Count > 1, "The fixture should be long enough to need chunking.");
            Assert.All(chunks, chunk => Assert.True(chunk.Length <= 40));
            Assert.Equal(payload, ElementMetadataCodec.Unchunk(chunks));
        }

        [Fact]
        public void Chunking_never_produces_an_empty_list()
        {
            Assert.Single(ElementMetadataCodec.Chunk(string.Empty));
        }

        [Fact]
        public void The_payload_stays_small_enough_to_write_onto_thousands_of_objects()
        {
            // Every generated object carries one of these. A compact payload is what keeps a drawing
            // with several thousand members from bloating.
            string payload = ElementMetadataCodec.Encode(Sample(), "Demo v1.0", "1.0.0");

            Assert.True(payload.Length < 1200, $"The payload grew to {payload.Length} characters.");
        }

        [Fact]
        public void Two_elements_that_would_produce_identical_geometry_share_a_block_name()
        {
            // This is what makes a thousand identical bolts cost one block definition.
            ElementDefinition first = TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: 6000m);
            ElementDefinition second = TestData.Element(ElementCategory.ISection, TestData.Ipe300, lengthMm: 6000m);

            Assert.Equal(
                BlockNaming.BlockNameFor(first, RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(second, RepresentationMode.Solid3D));
        }

        [Theory]
        [InlineData(6000, 6001)]
        public void Different_lengths_produce_different_block_names(int first, int second) =>
            Assert.NotEqual(
                BlockNaming.BlockNameFor(TestData.Element(ElementCategory.ISection, TestData.Ipe300, first), RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(TestData.Element(ElementCategory.ISection, TestData.Ipe300, second), RepresentationMode.Solid3D));

        [Fact]
        public void An_element_edited_in_custom_mode_gets_its_own_block_definition()
        {
            // Otherwise editing one member would silently reshape every other member sharing its
            // designation.
            Dictionary<string, decimal> altered = new(TestData.Ipe300) { ["tw"] = 12m };

            Assert.NotEqual(
                BlockNaming.BlockNameFor(TestData.Element(ElementCategory.ISection, TestData.Ipe300), RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(TestData.Element(ElementCategory.ISection, altered, isCustom: true), RepresentationMode.Solid3D));
        }

        [Fact]
        public void Position_and_quantity_do_not_affect_the_block_name()
        {
            // Moving a beam must not create a second definition.
            ElementDefinition atOrigin = TestData.Element(ElementCategory.ISection, TestData.Ipe300, quantity: 1);
            ElementDefinition elsewhere = atOrigin.With(
                quantity: 25,
                placements: new[] { new Placement(new Vec3(5000d, 900d, 12d), Vec3.UnitY, 90d) },
                layer: "another-layer");

            Assert.Equal(
                BlockNaming.BlockNameFor(atOrigin, RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(elsewhere, RepresentationMode.Solid3D));
        }

        [Fact]
        public void Detail_level_and_representation_change_the_block_name()
        {
            ElementDefinition element = TestData.Element(ElementCategory.ISection, TestData.Ipe300);

            Assert.NotEqual(
                BlockNaming.BlockNameFor(element, RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(element, RepresentationMode.Profile2D));

            Assert.NotEqual(
                BlockNaming.BlockNameFor(element, RepresentationMode.Solid3D),
                BlockNaming.BlockNameFor(
                    TestData.Element(ElementCategory.ISection, TestData.Ipe300, detail: DetailLevel.Simplified),
                    RepresentationMode.Solid3D));
        }

        [Fact]
        public void The_shape_hash_is_stable_rather_than_randomised_per_process()
        {
            // The framework's own string hash is randomised per process. Using it would produce a
            // different block name every time AutoCAD restarted, duplicating every definition.
            ElementDefinition element = TestData.Element(ElementCategory.ISection, TestData.Ipe300);

            // A fixed expected value is the point of this test. If a change to the hash makes it
            // fail, every drawing already produced would start generating duplicate block
            // definitions on its next edit, so the value must only ever change deliberately.
            Assert.Equal("7078e78a", BlockNaming.ShapeHash(element));
            Assert.Equal(8, BlockNaming.ShapeHash(element).Length);
        }

        [Theory]
        [InlineData("L 100x100x10", "L-100x100x10")]
        [InlineData("W12X26", "W12X26")]
        [InlineData("CHS 48.3 x 3.2", "CHS-48.3-x-3.2")]
        [InlineData("IPE 300", "IPE-300")]
        [InlineData("Angle <special>", "Angle--special-")]
        [InlineData("", "x")]
        public void Designations_are_reduced_to_characters_AutoCAD_accepts_in_a_block_name(
            string designation, string expected) =>
            Assert.Equal(expected, BlockNaming.Sanitise(designation));

        [Fact]
        public void A_block_name_carries_the_prefix_and_stays_inside_the_length_limit()
        {
            ElementDefinition element = TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, designation: new string('X', 400));

            string name = BlockNaming.BlockNameFor(element, RepresentationMode.Solid3D);

            Assert.StartsWith("SG_", name);
            Assert.True(name.Length <= 255);
        }
    }
}
