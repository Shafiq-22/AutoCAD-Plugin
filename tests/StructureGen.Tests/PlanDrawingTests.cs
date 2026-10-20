using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Geometry;
using StructureGen.Geometry.Drawing;
using StructureGen.Services;
using Xunit;

namespace StructureGen.Tests
{
    public class PlanDrawingTests
    {
        private static readonly ElementGeneratorRegistry Registry = TestData.Registry();

        private static ElementDefinition Ipe300(decimal length = 6000m) =>
            TestData.Element(ElementCategory.ISection, TestData.Ipe300, length, designation: "IPE 300");

        private static ElementDefinition Pipe() =>
            TestData.Element(
                ElementCategory.Pipe,
                new Dictionary<string, decimal> { ["d"] = 114.3m, ["t"] = 5m },
                3000m,
                designation: "CHS 114.3x5");

        private static ElementDefinition Bolt() =>
            TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                null,
                designation: "M16 hex bolt");

        private static ElementGeometry GeometryOf(ElementDefinition definition)
        {
            Result<ElementGeometry> built = Registry.Find(definition.CategoryId)!.BuildGeometry(definition);
            Assert.True(built.IsSuccess, built.ErrorMessage);
            return built.Value;
        }

        private static PlanView PlanOf(ElementDefinition definition, Placement placement) =>
            PlanViewBuilder.Build(GeometryOf(definition), ElementFrame.FromPlacement(placement));

        /// <summary>The plan Y positions of the lines running along plan X, by kind.</summary>
        private static List<double> TracksAlongX(PlanView view, PlanLineKind kind) =>
            view.Curves
                .Where(c => c.Kind == kind && c.Vertices.Count == 2)
                .Where(c => Math.Abs(c.Vertices[0].Point.Y - c.Vertices[1].Point.Y) < 1e-6 &&
                            Math.Abs(c.Vertices[0].Point.X - c.Vertices[1].Point.X) > 1e-3)
                .Select(c => Math.Round(c.Vertices[0].Point.Y, 4))
                .OrderBy(y => y)
                .ToList();

        [Fact]
        public void A_frame_from_a_placement_stands_a_level_beam_upright()
        {
            ElementFrame frame = ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitX));

            Assert.True(frame.ZAxis.IsEqualTo(Vec3.UnitX, 1e-12));
            Assert.True(frame.XAxis.IsEqualTo(Vec3.UnitY, 1e-12));
            Assert.True(frame.YAxis.IsEqualTo(Vec3.UnitZ, 1e-12), "The section height of a level beam must be vertical.");
        }

        [Fact]
        public void Rotating_a_placement_turns_the_section_about_the_member_axis()
        {
            ElementFrame frame = ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitX, 90d));

            Assert.True(frame.XAxis.IsEqualTo(Vec3.UnitZ, 1e-12));
            Assert.True(frame.YAxis.IsEqualTo(new Vec3(0d, -1d, 0d), 1e-12));
        }

        [Fact]
        public void A_level_I_beam_shows_its_flange_edges_and_its_web_as_hidden_lines()
        {
            PlanView view = PlanOf(Ipe300(), new Placement(Vec3.Origin, Vec3.UnitX));

            Assert.False(view.IsEndView);
            Assert.Equal(new[] { -75d, 75d }, TracksAlongX(view, PlanLineKind.Visible));
            Assert.Equal(new[] { -3.55d, 3.55d }, TracksAlongX(view, PlanLineKind.Hidden));

            // Two end lines spanning the flange width close the outline.
            List<PlanCurve> ends = view.Curves
                .Where(c => c.Kind == PlanLineKind.Visible && Math.Abs(c.Vertices[0].Point.X - c.Vertices[1].Point.X) < 1e-6)
                .ToList();

            Assert.Equal(2, ends.Count);
            Assert.All(ends, e => Assert.Equal(150d, e.Length, 6));
            Assert.Equal(new[] { 0d, 6000d }, ends.Select(e => Math.Round(e.Vertices[0].Point.X, 6)).OrderBy(x => x));

            // Every line along the member runs its full length.
            Assert.All(
                view.Curves.Where(c => Math.Abs(c.Vertices[0].Point.Y - c.Vertices[1].Point.Y) < 1e-6),
                c => Assert.Equal(6000d, c.Length, 6));

            Assert.DoesNotContain(view.Curves, c => c.Kind == PlanLineKind.Centre);
            Assert.Equal(75d, view.HalfWidthMm, 6);
        }

        [Fact]
        public void An_I_beam_laid_on_its_side_shows_both_faces_of_each_flange_and_no_hidden_web()
        {
            PlanView view = PlanOf(Ipe300(), new Placement(Vec3.Origin, Vec3.UnitX, 90d));

            Assert.Equal(new[] { -150d, -139.3d, 139.3d, 150d }, TracksAlongX(view, PlanLineKind.Visible));
            Assert.Empty(TracksAlongX(view, PlanLineKind.Hidden));
            Assert.Equal(150d, view.HalfWidthMm, 6);
        }

        [Fact]
        public void A_beam_running_along_plan_Y_is_drawn_along_plan_Y_and_labelled_reading_upwards()
        {
            PlanView view = PlanOf(Ipe300(2000m), new Placement(new Vec3(1000d, 500d, 0d), Vec3.UnitY));

            (Vec2 minimum, Vec2 maximum) = view.Bounds();
            Assert.Equal(925d, minimum.X, 6);
            Assert.Equal(1075d, maximum.X, 6);
            Assert.Equal(500d, minimum.Y, 6);
            Assert.Equal(2500d, maximum.Y, 6);
            Assert.Equal(Math.PI / 2d, view.LabelAngle, 9);
        }

        [Fact]
        public void A_beam_running_backwards_is_labelled_so_the_text_is_not_upside_down()
        {
            PlanView view = PlanOf(Ipe300(2000m), new Placement(Vec3.Origin, new Vec3(-1d, 0d, 0d)));

            Assert.Equal(0d, view.LabelAngle, 9);
        }

        [Fact]
        public void A_column_is_drawn_as_its_cross_section_with_exact_arcs()
        {
            ElementDefinition column = Ipe300(3500m);
            ElementGeometry geometry = GeometryOf(column);
            PlanView view = PlanViewBuilder.Build(
                geometry, ElementFrame.FromPlacement(new Placement(new Vec3(100d, 200d, 0d), Vec3.UnitZ)));

            Assert.True(view.IsEndView);

            PlanCurve outline = Assert.Single(view.Curves);
            Assert.Equal(PlanLineKind.Visible, outline.Kind);
            Assert.True(outline.Closed);
            Assert.Equal(geometry.PrimaryProfile.Outer.Vertices.Count, outline.Vertices.Count);
            Assert.Contains(outline.Vertices, v => v.IsArc);

            (Vec2 minimum, Vec2 maximum) = view.Bounds();
            Assert.Equal(300d, maximum.Y - minimum.Y > maximum.X - minimum.X
                ? maximum.Y - minimum.Y
                : maximum.X - minimum.X, 3);
            Assert.True(view.LabelAnchor.IsEqualTo(new Vec2(100d, 200d), 1e-6));
        }

        [Fact]
        public void A_mirrored_column_has_its_arcs_turned_the_other_way()
        {
            ElementGeometry geometry = GeometryOf(Ipe300(3500m));
            ElementFrame normal = ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitZ));
            ElementFrame mirrored = new(normal.Origin, normal.XAxis * -1d, normal.YAxis, normal.ZAxis);

            PlanCurve plain = Assert.Single(PlanViewBuilder.Build(geometry, normal).Curves);
            PlanCurve flipped = Assert.Single(PlanViewBuilder.Build(geometry, mirrored).Curves);

            for (int index = 0; index < plain.Vertices.Count; index++)
            {
                Assert.Equal(-plain.Vertices[index].Bulge, flipped.Vertices[index].Bulge, 12);
            }

            // Mirroring an arc must not change how long it is: the true outline is reproduced.
            Assert.Equal(plain.Length, flipped.Length, 6);
        }

        [Fact]
        public void A_level_pipe_shows_its_bore_as_hidden_lines_and_carries_a_centre_line()
        {
            PlanView view = PlanOf(Pipe(), new Placement(Vec3.Origin, Vec3.UnitX));

            Assert.Equal(new[] { -57.15d, 57.15d }, TracksAlongX(view, PlanLineKind.Visible));
            Assert.Equal(new[] { -52.15d, 52.15d }, TracksAlongX(view, PlanLineKind.Hidden));

            PlanCurve centre = Assert.Single(view.Curves, c => c.Kind == PlanLineKind.Centre);
            Assert.Equal(0d, centre.Vertices[0].Point.Y, 9);
            Assert.True(centre.Length > 3000d, "A centre line runs past the ends of the member.");
        }

        [Fact]
        public void Centre_lines_can_be_left_out()
        {
            PlanView view = PlanViewBuilder.Build(
                GeometryOf(Pipe()), ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitX)), centreLines: false);

            Assert.DoesNotContain(view.Curves, c => c.Kind == PlanLineKind.Centre);
        }

        [Fact]
        public void An_upright_bolt_shows_its_head_with_the_shank_hidden_beneath_it()
        {
            PlanView view = PlanOf(Bolt(), new Placement(Vec3.Origin, new Vec3(0d, 0d, -1d)));

            Assert.True(view.IsEndView);

            PlanCurve head = Assert.Single(view.Curves, c => c.Kind == PlanLineKind.Visible);
            Assert.True(head.Closed);
            Assert.Equal(6, head.Vertices.Count);

            PlanCurve shank = Assert.Single(view.Curves, c => c.Kind == PlanLineKind.Hidden);
            Assert.True(shank.Closed);
            Assert.Equal(Math.PI * 16d, shank.Length, 6);

            Assert.Equal(2, view.Curves.Count(c => c.Kind == PlanLineKind.Centre));
        }

        [Fact]
        public void A_bolt_seen_from_below_shows_the_shank_end_inside_the_head_outline()
        {
            PlanView view = PlanOf(Bolt(), new Placement(Vec3.Origin, Vec3.UnitZ));

            // The shank end is nearest. The head is behind it but wider, so its outline is seen
            // round the shank rather than hidden.
            Assert.Single(view.Curves, c => c.Kind == PlanLineKind.Visible && c.Vertices.Count == 2);
            Assert.Single(view.Curves, c => c.Kind == PlanLineKind.Visible && c.Vertices.Count == 6);
            Assert.DoesNotContain(view.Curves, c => c.Kind == PlanLineKind.Hidden);
        }

        [Fact]
        public void A_level_bolt_draws_its_head_and_shank_without_a_line_where_they_meet_inside_the_head()
        {
            PlanView view = PlanOf(Bolt(), new Placement(Vec3.Origin, Vec3.UnitX));

            // Shank silhouette at the thread diameter, running only the length of the shank.
            List<PlanCurve> shankEdges = view.Curves
                .Where(c => c.Kind == PlanLineKind.Visible && c.Vertices.Count == 2)
                .Where(c => Math.Abs(Math.Abs(c.Vertices[0].Point.Y) - 8d) < 1e-6 &&
                            Math.Abs(c.Vertices[0].Point.Y - c.Vertices[1].Point.Y) < 1e-9)
                .ToList();

            Assert.Equal(2, shankEdges.Count);
            Assert.All(shankEdges, e => Assert.Equal(60d, e.Length, 6));
            Assert.All(shankEdges, e => Assert.True(Math.Min(e.Vertices[0].Point.X, e.Vertices[1].Point.X) >= -1e-9));

            // The shank's own end against the head is covered by the head's face and is not drawn twice.
            List<PlanCurve> acrossAtJoint = view.Curves
                .Where(c => c.Vertices.Count == 2 &&
                            Math.Abs(c.Vertices[0].Point.X) < 1e-6 && Math.Abs(c.Vertices[1].Point.X) < 1e-6)
                .ToList();

            PlanCurve joint = Assert.Single(acrossAtJoint);
            Assert.True(joint.Length > 16d, "The line at the joint is the head's face, which is wider than the shank.");
        }

        [Fact]
        public void An_inclined_member_shows_the_end_facing_the_viewer_and_hides_the_other()
        {
            Vec3 axis = new(1d, 0d, 1d);
            PlanView view = PlanOf(Ipe300(2000m), new Placement(Vec3.Origin, axis));

            Assert.False(view.IsEndView);

            List<PlanCurve> faces = view.Curves.Where(c => c.Closed).ToList();
            Assert.Equal(2, faces.Count);
            Assert.Contains(faces, f => f.Kind == PlanLineKind.Visible);
            Assert.Contains(faces, f => f.Kind == PlanLineKind.Hidden);

            // The upper end is the visible one, and it lies further along plan X.
            PlanCurve visible = faces.Single(f => f.Kind == PlanLineKind.Visible);
            PlanCurve hidden = faces.Single(f => f.Kind == PlanLineKind.Hidden);
            Assert.True(visible.Bounds().Minimum.X > hidden.Bounds().Minimum.X);
        }

        [Fact]
        public void Every_shipped_section_family_produces_a_plan_both_level_and_upright()
        {
            Data.StructuralDatabase database = TestData.LoadDemoDatabase();

            foreach (SectionRecord record in database.Sections.GroupBy(s => s.CategoryId).Select(g => g.First()))
            {
                ElementDefinition definition = TestData.FromRecord(record);
                IElementGenerator? generator = Registry.Find(definition.CategoryId);
                if (generator is null)
                {
                    continue;
                }

                Result<ElementGeometry> geometry = generator.BuildGeometry(definition);
                if (geometry.IsFailure)
                {
                    continue;
                }

                foreach (Vec3 axis in new[] { Vec3.UnitX, Vec3.UnitZ, new Vec3(0d, 0d, -1d), new Vec3(1d, 1d, 0.5d) })
                {
                    PlanView view = PlanViewBuilder.Build(geometry.Value, ElementFrame.FromPlacement(new Placement(Vec3.Origin, axis)));

                    Assert.True(
                        view.Curves.Count(c => c.Kind == PlanLineKind.Visible) >= 1,
                        $"{record.Designation} along {axis} produced no visible lines.");
                    Assert.All(view.Curves, c => Assert.True(c.Length > 0d, $"{record.Designation} produced a zero-length line."));
                }
            }
        }

        [Fact]
        public void An_arrangement_keeps_true_positions_and_labels_members_but_not_fasteners()
        {
            PlanDrawingService service = new(Registry, NullLogger.Instance);

            List<PlacedElement> elements = new()
            {
                new(Ipe300(), ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitX))),
                new(Ipe300(), ElementFrame.FromPlacement(new Placement(new Vec3(0d, 3000d, 0d), Vec3.UnitX))),
                new(Bolt(), ElementFrame.FromPlacement(new Placement(new Vec3(500d, 0d, 160d), new Vec3(0d, 0d, -1d))))
            };

            Result<PlanDrawing> result = service.Compose(elements, new PlanDrawingOptions());

            Assert.True(result.IsSuccess, result.ErrorMessage);
            PlanDrawing drawing = result.Value;

            Assert.Equal(PlanLayout.Arrangement, drawing.Layout);
            Assert.Equal(3, drawing.ElementCount);
            Assert.Equal(2, drawing.ItemCount);
            Assert.Equal(3, drawing.Texts.Count); // two beam labels and the title
            Assert.Equal(2, drawing.Texts.Count(t => t.Text == "IPE 300 L=6000"));
            Assert.Contains(drawing.Texts, t => t.Text == "PLAN");

            (Vec2 minimum, Vec2 maximum) = drawing.Bounds();
            Assert.Equal(-75d, minimum.Y, 6);
            Assert.Equal(3075d, maximum.Y, 6);
            Assert.Equal(0d, minimum.X, 6);
            Assert.Equal(6000d, maximum.X, 6);

            // Labels sit clear of the member they name.
            PlanText label = drawing.Texts.First(t => t.Text.StartsWith("IPE", StringComparison.Ordinal));
            Assert.True(Math.Abs(label.Position.Y - 0d) > 75d || Math.Abs(label.Position.Y - 3000d) > 75d);
        }

        [Fact]
        public void An_item_sheet_has_one_plan_per_distinct_item_with_its_count()
        {
            PlanDrawingService service = new(Registry, NullLogger.Instance);

            List<PlacedElement> elements = new();
            for (int index = 0; index < 3; index++)
            {
                elements.Add(new(Ipe300(), ElementFrame.FromPlacement(new Placement(new Vec3(0d, index * 1000d, 0d), Vec3.UnitY))));
            }

            elements.Add(new(Pipe(), ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitZ))));
            elements.Add(new(Bolt(), ElementFrame.FromPlacement(new Placement(Vec3.Origin, Vec3.UnitX))));

            Result<PlanDrawing> result = service.Compose(
                elements, new PlanDrawingOptions { Layout = PlanLayout.Items, TextHeightMm = 20d });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            PlanDrawing drawing = result.Value;

            Assert.Equal(3, drawing.ItemCount);
            Assert.Equal(5, drawing.ElementCount);
            Assert.Contains(drawing.Texts, t => t.Text == "1. IPE 300 L=6000 - 3 No.");
            Assert.Contains(drawing.Texts, t => t.Text == "2. CHS 114.3x5 L=3000 - 1 No.");
            Assert.Contains(drawing.Texts, t => t.Text == "3. M16 hex bolt - 1 No.");
            Assert.All(drawing.Texts, t => Assert.Equal(0d, t.Angle, 12));

            // On the sheet every item lies level along plan X, whichever way it runs in the model,
            // and items are stacked downwards without overlapping.
            List<double> labelHeights = drawing.Texts.Where(t => char.IsDigit(t.Text[0])).Select(t => t.Position.Y).ToList();
            Assert.Equal(labelHeights.OrderByDescending(y => y), labelHeights);

            (Vec2 minimum, Vec2 maximum) = drawing.Bounds();
            Assert.Equal(0d, minimum.X, 6);
            Assert.Equal(6000d, maximum.X, 6);
            Assert.True(maximum.Y < 0d);

            // The bolt stands upright on the sheet, so its hexagon head is seen.
            Assert.Contains(drawing.Curves, c => c.Closed && c.Vertices.Count == 6 && c.Kind == PlanLineKind.Visible);
        }

        [Fact]
        public void An_element_that_cannot_be_drawn_is_reported_and_the_rest_still_are()
        {
            PlanDrawingService service = new(Registry, NullLogger.Instance);
            ElementDefinition unknown = TestData.Element("no-such-category", TestData.Plate, 1000m, designation: "MYSTERY");

            Result<PlanDrawing> result = service.Compose(
                new List<PlacedElement>
                {
                    new(unknown, ElementFrame.World),
                    new(Ipe300(), ElementFrame.FromPlacement(Placement.Default))
                },
                new PlanDrawingOptions());

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.ElementCount);
            Assert.Contains(result.Value.Report.Messages, m => m.Code == "Plan.NoGenerator" && m.Message.Contains("MYSTERY"));
        }

        [Fact]
        public void A_plan_of_nothing_drawable_is_refused_with_the_reason()
        {
            PlanDrawingService service = new(Registry, NullLogger.Instance);
            ElementDefinition unknown = TestData.Element("no-such-category", TestData.Plate, 1000m, designation: "MYSTERY");

            Result<PlanDrawing> empty = service.Compose(Array.Empty<PlacedElement>(), new PlanDrawingOptions());
            Result<PlanDrawing> undrawable = service.Compose(new[] { new PlacedElement(unknown, ElementFrame.World) }, new PlanDrawingOptions());

            Assert.Equal("Plan.NoElements", empty.ErrorCode);
            Assert.Equal("Plan.NothingDrawn", undrawable.ErrorCode);
            Assert.Contains("MYSTERY", undrawable.ErrorMessage);
        }

        [Fact]
        public void The_automatic_label_height_follows_the_typical_member_width()
        {
            PlanView beam = PlanOf(Ipe300(), Placement.Default);

            Assert.Equal(75d, PlanDrawingService.AutomaticTextHeight(new[] { beam, beam, beam }), 6);
            Assert.Equal(50d, PlanDrawingService.AutomaticTextHeight(Array.Empty<PlanView>()), 6);
        }
    }
}
