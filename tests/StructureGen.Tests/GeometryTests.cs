using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Data;
using StructureGen.Geometry;
using Xunit;

namespace StructureGen.Tests
{
    public class GeometryTests
    {
        private static readonly ElementGeneratorRegistry Registry = TestData.Registry();

        [Fact]
        public void A_rounded_corner_produces_the_arc_a_fillet_should_have()
        {
            // A right angle rounded at radius two: the tangent points sit two units back along each
            // edge, and a quarter turn anticlockwise has a bulge of tan(22.5 degrees).
            bool rounded = ProfileLoopBuilder.TryRoundCorner(
                new Vec2(0d, 0d), new Vec2(10d, 0d), new Vec2(10d, 10d), 2d,
                out ProfileVertex start, out ProfileVertex end, out bool clamped);

            Assert.True(rounded);
            Assert.False(clamped);
            Assert.True(start.Point.IsEqualTo(new Vec2(8d, 0d), 1e-9));
            Assert.True(end.Point.IsEqualTo(new Vec2(10d, 2d), 1e-9));
            Assert.Equal(Math.Tan(Math.PI / 8d), start.Bulge, 12);
        }

        [Fact]
        public void A_concave_corner_curves_the_other_way()
        {
            bool rounded = ProfileLoopBuilder.TryRoundCorner(
                new Vec2(10d, 0d), new Vec2(0d, 0d), new Vec2(0d, 10d), 2d,
                out ProfileVertex start, out _, out _);

            Assert.True(rounded);
            Assert.True(start.Bulge < 0d, "A fillet turning into the material must have a negative bulge.");
        }

        [Fact]
        public void A_radius_too_large_for_its_edges_is_reduced_rather_than_producing_broken_geometry()
        {
            bool rounded = ProfileLoopBuilder.TryRoundCorner(
                new Vec2(0d, 0d), new Vec2(4d, 0d), new Vec2(4d, 4d), 50d,
                out ProfileVertex start, out ProfileVertex end, out bool clamped);

            Assert.True(rounded);
            Assert.True(clamped);
            Assert.True(start.Point.X >= 2d, "The fillet must not consume more than half of its edge.");
            Assert.True(end.Point.Y <= 2d);
        }

        [Fact]
        public void A_circle_is_exact_rather_than_a_polygon()
        {
            ProfileLoop circle = ProfileLoopBuilder.Circle(Vec2.Zero, 50d);

            Assert.Equal(2, circle.Vertices.Count);
            Assert.Equal(Math.PI * 2500d, circle.Area, 9);
            Assert.True(circle.IsAnticlockwise);
        }

        [Fact]
        public void A_void_circle_is_wound_the_opposite_way_so_it_subtracts()
        {
            ProfileLoop hole = ProfileLoopBuilder.Circle(Vec2.Zero, 10d, isOuter: false);

            Assert.False(hole.IsAnticlockwise);
            Assert.Equal(Math.PI * 100d, hole.Area, 9);
        }

        [Fact]
        public void A_rounded_rectangle_loses_exactly_the_corner_material()
        {
            ProfileLoop rectangle = ProfileLoopBuilder.CentredRectangle(100d, 60d, 10d);

            // Four quarter circles of radius ten replace four square corners of side ten.
            double expected = (100d * 60d) - ((4d - Math.PI) * 100d);
            Assert.Equal(expected, rectangle.Area, 9);
        }

        [Fact]
        public void A_hexagon_is_dimensioned_across_its_flats()
        {
            ProfileLoop hexagon = ProfileLoopBuilder.RegularPolygonAcrossFlats(Vec2.Zero, 24d);

            (Vec2 minimum, Vec2 maximum) = hexagon.Bounds();

            Assert.Equal(6, hexagon.Vertices.Count);
            Assert.Equal(24d, maximum.X - minimum.X, 9);
            Assert.Equal(24d * 24d * Math.Sqrt(3d) / 2d, hexagon.Area, 9);
        }

        [Fact]
        public void Reversing_a_loop_preserves_its_shape()
        {
            ProfileLoop loop = ProfileLoopBuilder.CentredRectangle(100d, 60d, 10d);
            ProfileLoop reversed = loop.Reversed();

            Assert.Equal(loop.Area, reversed.Area, 9);
            Assert.NotEqual(loop.IsAnticlockwise, reversed.IsAnticlockwise);
        }

        [Fact]
        public void An_i_section_matches_its_published_area()
        {
            // IPE 300 is published with an area of 5380 mm2. Computing 5381 from the outline, with
            // the four root fillets included exactly rather than approximated, is the strongest
            // check available that the generated shape is the real profile.
            ElementGeometry geometry = Build(ElementCategory.ISection, TestData.Ipe300);

            Assert.Equal(5381.2d, geometry.PrimaryProfile.AreaMm2, 1);
            Assert.Equal(150d, geometry.PrimaryProfile.WidthMm, 9);
            Assert.Equal(300d, geometry.PrimaryProfile.HeightMm, 9);
        }

        [Fact]
        public void Suppressing_the_fillets_removes_exactly_the_fillet_material()
        {
            ElementDefinition standard = TestData.Element(ElementCategory.ISection, TestData.Ipe300);
            ElementDefinition simplified = TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, detail: DetailLevel.Simplified);

            double filleted = Build(standard).PrimaryProfile.AreaMm2;
            double sharp = Build(simplified).PrimaryProfile.AreaMm2;

            // Two flanges, plus the web between them, with no fillets.
            double expectedSharp = (2d * 150d * 10.7d) + ((300d - (2d * 10.7d)) * 7.1d);
            Assert.Equal(expectedSharp, sharp, 6);

            // Four fillets of radius fifteen, each covering the area between a quarter circle and
            // its square corner.
            Assert.Equal(4d * 225d * (1d - (Math.PI / 4d)), filleted - sharp, 6);
        }

        [Fact]
        public void The_outer_contour_of_every_generated_profile_is_wound_anticlockwise()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();

            foreach (SectionRecord record in database.Sections)
            {
                ElementGeometry geometry = Build(Definition(record));

                Assert.True(
                    geometry.PrimaryProfile.Outer.IsAnticlockwise,
                    $"'{record.Designation}' produced an outer contour wound the wrong way.");

                Assert.All(geometry.PrimaryProfile.Voids, hole =>
                    Assert.False(hole.IsAnticlockwise, $"'{record.Designation}' produced a void wound the wrong way."));
            }
        }

        [Fact]
        public void Every_record_in_the_shipped_database_generates()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();
            List<string> failures = new();

            foreach (SectionRecord record in database.Sections)
            {
                IElementGenerator? generator = Registry.Find(record.CategoryId);

                if (generator is null)
                {
                    failures.Add($"{record.Designation}: no generator for '{record.CategoryId}'");
                    continue;
                }

                Result<ElementGeometry> result = generator.BuildGeometry(Definition(record));

                if (result.IsFailure)
                {
                    failures.Add($"{record.Designation}: {result.ErrorMessage}");
                }
                else if (result.Value.PrimaryProfile.AreaMm2 <= 0d)
                {
                    failures.Add($"{record.Designation}: produced an area of zero");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void Generated_areas_agree_with_the_areas_the_tables_publish()
        {
            // Every record that carries a published area is checked against the area computed from
            // the outline. Tapered flange channels are excluded and asserted separately: StructureGen
            // draws them with parallel flanges, which is a documented approximation.
            StructuralDatabase database = TestData.LoadDemoDatabase();
            List<string> failures = new();

            foreach (SectionRecord record in database.Sections)
            {
                if (record.Dimension(DimensionKeys.Area) is not { } published || published <= 0m)
                {
                    continue;
                }

                if (record.CategoryId == ElementCategory.Channel)
                {
                    continue;
                }

                double generated = Build(Definition(record)).PrimaryProfile.AreaMm2;
                double error = Math.Abs(generated - (double)published) / (double)published * 100d;

                if (error > 2d)
                {
                    failures.Add($"{record.Designation}: generated {generated:0.0} mm2 against a published {published:0.0} mm2 ({error:0.00}%)");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void Tapered_flange_channels_are_within_the_approximation_the_documentation_claims()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();

            foreach (SectionRecord record in database.Sections.Where(s => s.CategoryId == ElementCategory.Channel))
            {
                if (record.Dimension(DimensionKeys.Area) is not { } published)
                {
                    continue;
                }

                double generated = Build(Definition(record)).PrimaryProfile.AreaMm2;
                double error = Math.Abs(generated - (double)published) / (double)published * 100d;

                Assert.True(error < 3d, $"{record.Designation} deviates by {error:0.00}%, more than the documented approximation.");
                Assert.True(generated > (double)published, $"{record.Designation}: a parallel flange approximation should over-estimate, not under-estimate.");
            }
        }

        [Fact]
        public void A_pipe_is_a_ring_of_the_right_area()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Pipe, new Dictionary<string, decimal> { ["d"] = 114.3m, ["t"] = 5m });

            double expected = (Math.PI * 57.15d * 57.15d) - (Math.PI * 52.15d * 52.15d);

            Assert.Equal(expected, geometry.PrimaryProfile.AreaMm2, 6);
            Assert.Single(geometry.PrimaryProfile.Voids);
        }

        [Fact]
        public void A_hollow_section_derives_its_inner_corner_radius_from_the_wall()
        {
            ElementGeometry geometry = Build(
                ElementCategory.SquareHollowSection,
                new Dictionary<string, decimal> { ["h"] = 100m, ["b"] = 100m, ["t"] = 5m, ["ro"] = 10m });

            // Outer 100 square with radius 10, inner 90 square with radius 5.
            double outer = (100d * 100d) - ((4d - Math.PI) * 100d);
            double inner = (90d * 90d) - ((4d - Math.PI) * 25d);

            Assert.Equal(outer - inner, geometry.PrimaryProfile.AreaMm2, 6);
        }

        [Fact]
        public void An_equal_angle_needs_only_one_leg_dimension()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Angle,
                new Dictionary<string, decimal> { ["a"] = 100m, ["tt"] = 10m, ["r"] = 12m, ["r2"] = 6m });

            Assert.Equal(100d, geometry.PrimaryProfile.WidthMm, 6);
            Assert.Equal(100d, geometry.PrimaryProfile.HeightMm, 6);
        }

        [Fact]
        public void An_angle_is_centred_on_its_bounding_box_like_every_other_profile()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Angle, new Dictionary<string, decimal> { ["a"] = 100m, ["tt"] = 10m });

            (Vec2 minimum, Vec2 maximum) = geometry.PrimaryProfile.Bounds();

            Assert.Equal(0d, (minimum.X + maximum.X) / 2d, 6);
            Assert.Equal(0d, (minimum.Y + maximum.Y) / 2d, 6);
        }

        [Fact]
        public void A_bolt_is_a_head_over_a_shank_with_the_origin_under_the_head()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                lengthMm: null);

            Assert.Equal(2, geometry.Parts.Count);

            GeometryPart head = geometry.Parts.Single(p => p.Name == "head");
            GeometryPart shank = geometry.Parts.Single(p => p.Name == "shank");

            Assert.Equal(-10d, head.StartOffset, 9);
            Assert.Equal(0d, head.EndOffset, 9);
            Assert.Equal(0d, shank.StartOffset, 9);
            Assert.Equal(60d, shank.EndOffset, 9);
            Assert.Equal(70d, geometry.OverallLengthMm, 9);
        }

        [Fact]
        public void A_bolt_shows_its_thread_only_at_the_detailed_level_and_only_when_the_data_supports_it()
        {
            Dictionary<string, decimal> dimensions = new()
            {
                ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m, ["d3"] = 13.5m, ["ls"] = 22m
            };

            Assert.Equal(2, Build(TestData.Element(
                ElementCategory.Bolt, dimensions, lengthMm: null, detail: DetailLevel.Standard)).Parts.Count);

            ElementGeometry detailed = Build(TestData.Element(
                ElementCategory.Bolt, dimensions, lengthMm: null, detail: DetailLevel.Detailed));

            Assert.Equal(3, detailed.Parts.Count);
            Assert.Contains(detailed.Parts, p => p.Name == "thread");
        }

        [Fact]
        public void A_bolt_without_thread_data_is_a_plain_cylinder_even_at_the_detailed_level()
        {
            // Nothing is invented. Without a published minor diameter the shank stays full diameter.
            ElementGeometry detailed = Build(TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                lengthMm: null,
                detail: DetailLevel.Detailed));

            Assert.Equal(2, detailed.Parts.Count);
            Assert.DoesNotContain(detailed.Parts, p => p.Name == "thread");
        }

        [Fact]
        public void A_nut_is_a_hexagon_with_a_bore()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Nut,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["m"] = 14.8m },
                lengthMm: null);

            double hexagon = 24d * 24d * Math.Sqrt(3d) / 2d;
            double bore = Math.PI * 64d;

            Assert.Equal(hexagon - bore, geometry.PrimaryProfile.AreaMm2, 6);
            Assert.Equal(14.8d, geometry.Parts[0].Length, 9);
        }

        [Fact]
        public void A_washer_is_an_annulus()
        {
            ElementGeometry geometry = Build(
                ElementCategory.Washer,
                new Dictionary<string, decimal> { ["d1"] = 17m, ["d2"] = 30m, ["hw"] = 3m },
                lengthMm: null);

            Assert.Equal((Math.PI * 225d) - (Math.PI * 72.25d), geometry.PrimaryProfile.AreaMm2, 6);
        }

        [Fact]
        public void A_custom_record_can_introduce_a_shape_the_code_has_never_heard_of()
        {
            StructuralDatabase database = TestData.LoadDemoDatabase();
            SectionRecord custom = database.SectionsInCategory(ElementCategory.Custom).Single();

            Assert.True(custom.HasOutline);

            ElementGeneratorRegistry registry = TestData.Registry(() => database);
            Result<ElementGeometry> result = registry.Require(ElementCategory.Custom)
                .BuildGeometry(Definition(custom));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.Value.PrimaryProfile.AreaMm2 > 0d);
        }

        [Fact]
        public void Placement_derives_direction_and_length_from_two_points()
        {
            (Placement placement, decimal length) = Placement.FromEndPoints(
                new Vec3(0d, 0d, 0d), new Vec3(3000d, 4000d, 0d));

            Assert.Equal(5000m, length);
            Assert.True(placement.AxisDirection.IsEqualTo(new Vec3(0.6d, 0.8d, 0d), 1e-9));
        }

        [Fact]
        public void Two_coincident_points_are_refused_rather_than_producing_a_zero_length_member() =>
            Assert.Throws<ArgumentException>(() =>
                Placement.FromEndPoints(new Vec3(1d, 2d, 3d), new Vec3(1d, 2d, 3d)));

        [Fact]
        public void Elevation_lifts_the_insertion_point_without_changing_the_origin()
        {
            Placement placement = new(new Vec3(10d, 20d, 30d), Vec3.UnitX, 0d, 500d);

            Assert.Equal(30d, placement.Origin.Z);
            Assert.Equal(530d, placement.EffectiveOrigin.Z);
        }

        private static ElementDefinition Definition(SectionRecord record) => TestData.FromRecord(record);

        private static ElementGeometry Build(
            string categoryId, IReadOnlyDictionary<string, decimal> dimensions, decimal? lengthMm = 6000m) =>
            Build(TestData.Element(categoryId, dimensions, lengthMm));

        private static ElementGeometry Build(ElementDefinition definition)
        {
            Result<ElementGeometry> result = Registry.Require(definition.CategoryId).BuildGeometry(definition);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            return result.Value;
        }
    }
}
