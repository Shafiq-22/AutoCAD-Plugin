using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;

namespace StructureGen.Geometry.Drawing
{
    /// <summary>
    /// Projects an element's generated geometry onto the plan plane - world XY seen from above - and
    /// sorts every resulting line into visible, hidden or centre.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The work is done on the same CAD-neutral geometry the solids are built from, so the plan of a
    /// member agrees with its solid by construction and can be checked without AutoCAD.
    /// </para>
    /// <para>
    /// An element is a stack of prisms. Seen from above, a prism lying on its side shows one line
    /// along its length for every edge of its cross-section that faces the viewer edge-on: every
    /// corner, and every place a curved face turns away (a silhouette). A line is hidden when
    /// material lies between it and the viewer, which for an I section is how the web becomes the
    /// pair of dashed lines a drafter expects between the flange edges. Seen end on - a column, or a
    /// bolt standing upright - the plan is the cross-section itself, with any part covered by a part
    /// nearer the viewer drawn hidden.
    /// </para>
    /// <para>
    /// Members that are neither level nor plumb are drawn from the same rules. Their longitudinal
    /// edges are classified from the cross-section alone, which is exact along the run of the member
    /// and approximate within a section depth of its ends.
    /// </para>
    /// </remarks>
    public static class PlanViewBuilder
    {
        private const double FlattenStepDegrees = 3d;

        /// <summary>Builds the plan of one element.</summary>
        /// <param name="geometry">The element's generated geometry, in its local frame.</param>
        /// <param name="frame">Where the local frame sits in the world.</param>
        /// <param name="centreLines">Whether round members get a centre line or centre mark.</param>
        /// <returns>The plan.</returns>
        public static PlanView Build(ElementGeometry geometry, ElementFrame frame, bool centreLines = true)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            Context context = new(geometry, frame);

            return context.IsEndView
                ? BuildEndView(context, centreLines)
                : BuildSideView(context, centreLines);
        }

        // ------------------------------------------------------------------------------------------
        // Side view: the member lies across the line of sight.
        // ------------------------------------------------------------------------------------------

        private static PlanView BuildSideView(Context context, bool centreLines)
        {
            List<PlanCurve> curves = new();
            List<LongitudinalLine> lines = new();
            double size = context.Size;

            double[] breaks = context.Parts
                .SelectMany(p => new[] { p.Start, p.End })
                .OrderBy(w => w)
                .ToArray();

            foreach (PartData part in context.Parts)
            {
                List<double> cuts = new() { part.Start };
                cuts.AddRange(breaks.Where(w => w > part.Start + context.AxialTolerance && w < part.End - context.AxialTolerance));
                cuts.Add(part.End);
                cuts = Distinct(cuts, context.AxialTolerance);

                for (int index = 0; index < cuts.Count - 1; index++)
                {
                    double w0 = cuts[index];
                    double w1 = cuts[index + 1];

                    List<PartData> covering = context.Parts
                        .Where(other => !ReferenceEquals(other, part) &&
                                        other.Start <= w0 + context.AxialTolerance &&
                                        other.End >= w1 - context.AxialTolerance)
                        .ToList();

                    List<PartData> occluders = new(covering.Count + 1) { part };
                    occluders.AddRange(covering);

                    foreach (List<Vec2> group in GroupByAcross(part.EdgePoints, context))
                    {
                        PlanLineKind? kind = null;

                        foreach (Vec2 point in group)
                        {
                            if (covering.Any(other => context.IsInterior(point, other)))
                            {
                                continue;
                            }

                            bool visible = !context.IsOccluded(point, occluders);
                            kind = visible || kind == PlanLineKind.Visible ? PlanLineKind.Visible : PlanLineKind.Hidden;
                        }

                        if (kind.HasValue)
                        {
                            lines.Add(new LongitudinalLine(context.Across(group[0]), group[0], w0, w1, kind.Value));
                        }
                    }
                }
            }

            foreach (LongitudinalLine line in MergeLines(lines, context))
            {
                curves.Add(PlanCurve.Line(
                    line.Kind,
                    context.ToPlan(line.Point, line.From),
                    context.ToPlan(line.Point, line.To),
                    size));
            }

            AddEndFaces(context, curves);

            if (centreLines && context.RoundCentre is { } centre)
            {
                double overrun = context.RoundDiameter * 0.25d;
                curves.Add(PlanCurve.Line(
                    PlanLineKind.Centre,
                    context.ToPlan(centre, context.Minimum - overrun),
                    context.ToPlan(centre, context.Maximum + overrun),
                    size));
            }

            Vec2 primaryCentre = context.PrimaryCentre;
            Vec2 anchor = context.ToPlan(primaryCentre, (context.Minimum + context.Maximum) / 2d);
            Vec2 axis = context.ToPlan(primaryCentre, context.Maximum) - context.ToPlan(primaryCentre, context.Minimum);

            double halfWidth = context.Parts.Max(p => Math.Max(
                Math.Abs(p.AcrossMinimum - context.Across(primaryCentre)),
                Math.Abs(p.AcrossMaximum - context.Across(primaryCentre))));

            return new PlanView(curves, false, anchor, ReadableAngle(axis), halfWidth);
        }

        private static void AddEndFaces(Context context, List<PlanCurve> curves)
        {
            double tolerance = context.AxialTolerance;
            double across = context.AcrossTolerance;

            for (int index = 0; index < context.Parts.Count; index++)
            {
                PartData part = context.Parts[index];

                foreach (bool atEnd in new[] { false, true })
                {
                    double w = atEnd ? part.End : part.Start;
                    bool hidden = false;

                    for (int otherIndex = 0; otherIndex < context.Parts.Count && !hidden; otherIndex++)
                    {
                        if (otherIndex == index)
                        {
                            continue;
                        }

                        PartData other = context.Parts[otherIndex];
                        bool covers = other.AcrossMinimum <= part.AcrossMinimum + across &&
                                      other.AcrossMaximum >= part.AcrossMaximum - across;

                        if (!covers)
                        {
                            continue;
                        }

                        bool same = Math.Abs(other.AcrossMinimum - part.AcrossMinimum) <= across &&
                                    Math.Abs(other.AcrossMaximum - part.AcrossMaximum) <= across;

                        // A face buried inside a wider part is not an edge at all.
                        bool buried = other.Start < w - tolerance && other.End > w + tolerance;

                        // A face against a wider neighbour is drawn by the neighbour's face. When the
                        // two are the same width, the first of them draws it.
                        bool abutting = (atEnd ? Math.Abs(other.Start - w) <= tolerance : Math.Abs(other.End - w) <= tolerance) &&
                                        (!same || otherIndex < index);

                        hidden = buried || abutting;
                    }

                    if (hidden)
                    {
                        continue;
                    }

                    if (context.IsLevel)
                    {
                        curves.Add(PlanCurve.Line(
                            PlanLineKind.Visible,
                            context.ToPlan(part.AcrossMinimumPoint, w),
                            context.ToPlan(part.AcrossMaximumPoint, w),
                            context.Size));
                        continue;
                    }

                    // An inclined member shows its end faces foreshortened. The face turned towards
                    // the viewer is seen; the other is behind the member.
                    bool facesViewer = atEnd ? context.UpAlongAxis > 0d : context.UpAlongAxis < 0d;
                    PlanLineKind kind = facesViewer ? PlanLineKind.Visible : PlanLineKind.Hidden;

                    foreach (List<Vec2> loop in part.FlatLoops)
                    {
                        curves.Add(new PlanCurve(
                            kind,
                            loop.Select(p => new ProfileVertex(context.ToPlan(p, w))),
                            true,
                            context.Size));
                    }
                }
            }
        }

        private static IEnumerable<List<Vec2>> GroupByAcross(IReadOnlyList<Vec2> points, Context context)
        {
            List<Vec2> sorted = points.OrderBy(context.Across).ToList();
            List<Vec2>? group = null;
            double groupAcross = double.NaN;

            foreach (Vec2 point in sorted)
            {
                double across = context.Across(point);

                if (group is not null && Math.Abs(across - groupAcross) <= context.AcrossTolerance)
                {
                    group.Add(point);
                    continue;
                }

                if (group is not null)
                {
                    yield return group;
                }

                group = new List<Vec2> { point };
                groupAcross = across;
            }

            if (group is not null)
            {
                yield return group;
            }
        }

        private static IEnumerable<LongitudinalLine> MergeLines(List<LongitudinalLine> lines, Context context)
        {
            List<LongitudinalLine> merged = new();

            foreach (IGrouping<PlanLineKind, LongitudinalLine> byKind in lines.GroupBy(l => l.Kind))
            {
                foreach (List<LongitudinalLine> track in ClusterByAcross(byKind.ToList(), context))
                {
                    LongitudinalLine? current = null;

                    foreach (LongitudinalLine line in track.OrderBy(l => l.From))
                    {
                        if (current is not null && line.From <= current.To + context.AxialTolerance)
                        {
                            current = current with { To = Math.Max(current.To, line.To) };
                            continue;
                        }

                        if (current is not null)
                        {
                            merged.Add(current);
                        }

                        current = line;
                    }

                    if (current is not null)
                    {
                        merged.Add(current);
                    }
                }
            }

            // A hidden line lying under a visible one on the same track adds nothing but noise.
            List<LongitudinalLine> visible = merged.Where(l => l.Kind == PlanLineKind.Visible).ToList();

            return merged.Where(line => line.Kind == PlanLineKind.Visible || !visible.Any(v =>
                Math.Abs(v.Across - line.Across) <= context.AcrossTolerance &&
                v.From <= line.From + context.AxialTolerance &&
                v.To >= line.To - context.AxialTolerance));
        }

        private static IEnumerable<List<LongitudinalLine>> ClusterByAcross(List<LongitudinalLine> lines, Context context)
        {
            List<LongitudinalLine>? cluster = null;
            double clusterAcross = double.NaN;

            foreach (LongitudinalLine line in lines.OrderBy(l => l.Across))
            {
                if (cluster is not null && Math.Abs(line.Across - clusterAcross) <= context.AcrossTolerance)
                {
                    cluster.Add(line);
                    continue;
                }

                if (cluster is not null)
                {
                    yield return cluster;
                }

                cluster = new List<LongitudinalLine> { line };
                clusterAcross = line.Across;
            }

            if (cluster is not null)
            {
                yield return cluster;
            }
        }

        // ------------------------------------------------------------------------------------------
        // End view: the member is seen along its own axis.
        // ------------------------------------------------------------------------------------------

        private static PlanView BuildEndView(Context context, bool centreLines)
        {
            List<PlanCurve> curves = new();
            bool upIsPositive = context.UpAlongAxis > 0d;
            bool mirrored = context.IsMirroredInPlan;

            double Nearness(PartData part) => upIsPositive ? part.End : -part.Start;

            List<PartData> ordered = context.Parts.OrderByDescending(Nearness).ToList();
            HashSet<string> drawn = new(StringComparer.Ordinal);

            foreach (PartData part in ordered)
            {
                List<PartData> nearer = ordered
                    .Where(other => Nearness(other) > Nearness(part) + context.AxialTolerance)
                    .ToList();

                double face = upIsPositive ? part.End : part.Start;

                foreach (ProfileLoop loop in part.Part.Profile.Loops)
                {
                    int count = loop.Vertices.Count;
                    PlanLineKind[] kinds = new PlanLineKind[count];
                    bool[] skip = new bool[count];

                    for (int index = 0; index < count; index++)
                    {
                        ProfileVertex start = loop.Vertices[index];
                        Vec2 end = loop.Vertices[(index + 1) % count].Point;

                        string key = SegmentKey(start.Point, end, start.Bulge, context.AcrossTolerance);
                        skip[index] = !drawn.Add(key);

                        Vec2 middle = SegmentMiddle(start.Point, end, start.Bulge);
                        kinds[index] = nearer.Any(other => context.IsInterior(middle, other))
                            ? PlanLineKind.Hidden
                            : PlanLineKind.Visible;
                    }

                    curves.AddRange(RunsOf(loop, kinds, skip, context, face, mirrored));
                }
            }

            Vec2 centre = context.PrimaryCentre;
            Vec2 anchor = context.ToPlan(centre, 0d);

            if (centreLines && context.RoundCentre is { } round)
            {
                double half = context.RoundDiameter * 0.65d;
                Vec2 middle = context.ToPlan(round, 0d);
                curves.Add(PlanCurve.Line(PlanLineKind.Centre, middle - new Vec2(half, 0d), middle + new Vec2(half, 0d), context.Size));
                curves.Add(PlanCurve.Line(PlanLineKind.Centre, middle - new Vec2(0d, half), middle + new Vec2(0d, half), context.Size));
            }

            (Vec2 minimum, Vec2 maximum) = PlanView.BoundsOf(curves);
            double halfWidth = Math.Max(maximum.X - minimum.X, maximum.Y - minimum.Y) / 2d;

            return new PlanView(curves, true, anchor, 0d, halfWidth);
        }

        private static IEnumerable<PlanCurve> RunsOf(
            ProfileLoop loop, PlanLineKind[] kinds, bool[] skip, Context context, double face, bool mirrored)
        {
            int count = loop.Vertices.Count;

            ProfileVertex Mapped(int index, bool withBulge)
            {
                ProfileVertex vertex = loop.Vertices[index % count];
                double bulge = withBulge ? (mirrored ? -vertex.Bulge : vertex.Bulge) : 0d;
                return new ProfileVertex(context.ToPlan(vertex.Point, face), bulge);
            }

            if (skip.All(s => !s) && kinds.All(k => k == kinds[0]))
            {
                yield return new PlanCurve(
                    kinds[0], Enumerable.Range(0, count).Select(i => Mapped(i, true)), true, context.Size);
                yield break;
            }

            // Start the walk where a run begins, so no run is split across the end of the loop.
            int first = 0;
            for (int index = 0; index < count; index++)
            {
                int previous = (index - 1 + count) % count;
                if (skip[index] != skip[previous] || kinds[index] != kinds[previous])
                {
                    first = index;
                    break;
                }
            }

            List<ProfileVertex>? run = null;
            PlanLineKind runKind = PlanLineKind.Visible;

            for (int step = 0; step < count; step++)
            {
                int index = (first + step) % count;

                if (skip[index] || (run is not null && kinds[index] != runKind))
                {
                    if (run is not null)
                    {
                        run.Add(Mapped(index, false));
                        yield return new PlanCurve(runKind, run, false, context.Size);
                        run = null;
                    }

                    if (skip[index])
                    {
                        continue;
                    }
                }

                run ??= new List<ProfileVertex>();
                runKind = kinds[index];
                run.Add(Mapped(index, true));
            }

            if (run is not null)
            {
                run.Add(Mapped(first + count, false));
                yield return new PlanCurve(runKind, run, false, context.Size);
            }
        }

        private static Vec2 SegmentMiddle(Vec2 start, Vec2 end, double bulge)
        {
            if (!ArcMath.TryDescribe(start, end, bulge, out Vec2 centre, out double radius, out double angle, out double sweep))
            {
                return new Vec2((start.X + end.X) / 2d, (start.Y + end.Y) / 2d);
            }

            double middle = angle + (sweep / 2d);
            return new Vec2(centre.X + (radius * Math.Cos(middle)), centre.Y + (radius * Math.Sin(middle)));
        }

        private static string SegmentKey(Vec2 start, Vec2 end, double bulge, double tolerance)
        {
            // The same segment met from either direction is the same edge.
            string Round(Vec2 p) => $"{Math.Round(p.X / tolerance)},{Math.Round(p.Y / tolerance)}";
            string forward = $"{Round(start)}>{Round(end)}:{Math.Round(bulge, 6)}";
            string backward = $"{Round(end)}>{Round(start)}:{Math.Round(-bulge, 6)}";
            return string.CompareOrdinal(forward, backward) <= 0 ? forward : backward;
        }

        // ------------------------------------------------------------------------------------------
        // Shared helpers.
        // ------------------------------------------------------------------------------------------

        private static double ReadableAngle(Vec2 direction)
        {
            if (direction.Magnitude <= 1e-12)
            {
                return 0d;
            }

            double angle = Math.Atan2(direction.Y, direction.X);

            // Text should never read upside down.
            if (angle > (Math.PI / 2d) + 1e-9)
            {
                angle -= Math.PI;
            }
            else if (angle <= (-Math.PI / 2d) + 1e-9)
            {
                angle += Math.PI;
            }

            return angle;
        }

        private static List<double> Distinct(List<double> values, double tolerance)
        {
            List<double> result = new();

            foreach (double value in values.OrderBy(v => v))
            {
                if (result.Count == 0 || value - result[result.Count - 1] > tolerance)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private sealed record LongitudinalLine(double Across, Vec2 Point, double From, double To, PlanLineKind Kind);

        /// <summary>One part's cross-section prepared for the visibility tests.</summary>
        private sealed class PartData
        {
            public PartData(GeometryPart part, Context context)
            {
                Part = part;
                FlatLoops = part.Profile.Loops
                    .Select(l => ArcMath.Flatten(l.Vertices, true, FlattenStepDegrees))
                    .ToList();

                EdgePoints = context.IsEndView ? new List<Vec2>() : context.EdgePointsOf(part.Profile);

                List<Vec2> outer = ArcMath.Flatten(part.Profile.Outer.Vertices, true, 1d);
                AcrossMinimumPoint = outer.OrderBy(context.Across).First();
                AcrossMaximumPoint = outer.OrderBy(context.Across).Last();
                AcrossMinimum = context.Across(AcrossMinimumPoint);
                AcrossMaximum = context.Across(AcrossMaximumPoint);
            }

            public GeometryPart Part { get; }

            public double Start => Part.StartOffset;

            public double End => Part.EndOffset;

            public List<List<Vec2>> FlatLoops { get; }

            public List<Vec2> EdgePoints { get; }

            public Vec2 AcrossMinimumPoint { get; }

            public Vec2 AcrossMaximumPoint { get; }

            public double AcrossMinimum { get; }

            public double AcrossMaximum { get; }
        }

        /// <summary>The viewing arrangement for one element.</summary>
        private sealed class Context
        {
            private readonly ElementFrame _frame;

            public Context(ElementGeometry geometry, ElementFrame frame)
            {
                _frame = frame;

                // The world's up direction expressed in the element's own frame.
                Vec3 up = Vec3.UnitZ;
                double upX = Vec3.Dot(frame.XAxis, up);
                double upY = Vec3.Dot(frame.YAxis, up);
                UpAlongAxis = Vec3.Dot(frame.ZAxis, up);

                Vec2 inPlane = new(upX, upY);
                IsEndView = inPlane.Magnitude < 1e-4;

                Toward = IsEndView ? new Vec2(0d, 1d) : inPlane.Normalised();
                Sideways = new Vec2(-Toward.Y, Toward.X);
                IsLevel = Math.Abs(UpAlongAxis) < 1e-6;

                Size = geometry.Parts.Max(p => Math.Max(p.Profile.WidthMm, p.Profile.HeightMm));
                Size = Size > 0d ? Size : 1d;
                AcrossTolerance = Math.Max(1e-6, Size * 1e-5);
                AxialTolerance = Math.Max(1e-6, geometry.OverallLengthMm * 1e-9);
                Offset = Math.Max(1e-7, Size * 1e-6);
                OcclusionTolerance = Math.Max(1e-6, Size * 1e-4);

                // Mirrored in plan when the profile's X and Y axes land in plan with their order
                // reversed, which turns every arc the other way.
                Vec2 planX = new(frame.XAxis.X, frame.XAxis.Y);
                Vec2 planY = new(frame.YAxis.X, frame.YAxis.Y);
                IsMirroredInPlan = Vec2.Cross(planX, planY) < 0d;

                Parts = geometry.Parts.Select(p => new PartData(p, this)).ToList();
                Minimum = geometry.Parts.Min(p => p.StartOffset);
                Maximum = geometry.Parts.Max(p => p.EndOffset);

                (Vec2 low, Vec2 high) = geometry.PrimaryProfile.Bounds();
                PrimaryCentre = new Vec2((low.X + high.X) / 2d, (low.Y + high.Y) / 2d);

                ProfileLoop primaryOuter = geometry.PrimaryProfile.Outer;
                if (primaryOuter.Vertices.Count == 2 &&
                    primaryOuter.Vertices.All(v => Math.Abs(Math.Abs(v.Bulge) - 1d) < 1e-9))
                {
                    RoundCentre = PrimaryCentre;
                    RoundDiameter = Vec2.Distance(primaryOuter.Vertices[0].Point, primaryOuter.Vertices[1].Point);
                }
            }

            public bool IsEndView { get; }

            public bool IsLevel { get; }

            public bool IsMirroredInPlan { get; }

            /// <summary>Gets how far world up points along the member axis.</summary>
            public double UpAlongAxis { get; }

            /// <summary>Gets the in-section direction pointing towards the viewer.</summary>
            public Vec2 Toward { get; }

            /// <summary>Gets the in-section direction across the line of sight.</summary>
            public Vec2 Sideways { get; }

            public double Size { get; }

            public double AcrossTolerance { get; }

            public double AxialTolerance { get; }

            public double Offset { get; }

            public double OcclusionTolerance { get; }

            public List<PartData> Parts { get; }

            public double Minimum { get; }

            public double Maximum { get; }

            public Vec2 PrimaryCentre { get; }

            public Vec2? RoundCentre { get; }

            public double RoundDiameter { get; }

            public double Across(Vec2 point) => Vec2.Dot(point, Sideways);

            public Vec2 ToPlan(Vec2 section, double along)
            {
                Vec3 world = _frame.ToWorld(section.X, section.Y, along);
                return new Vec2(world.X, world.Y);
            }

            /// <summary>
            /// Finds the points of a cross-section that become lines when the section is seen
            /// edge-on: corners, and places where the outline turns parallel to the line of sight.
            /// </summary>
            public List<Vec2> EdgePointsOf(ProfileGeometry profile)
            {
                List<Vec2> points = new();
                const double cornerSine = 0.0087d; // half a degree
                const double parallel = 1e-7;

                foreach (ProfileLoop loop in profile.Loops)
                {
                    int count = loop.Vertices.Count;

                    for (int index = 0; index < count; index++)
                    {
                        ProfileVertex previous = loop.Vertices[(index - 1 + count) % count];
                        ProfileVertex current = loop.Vertices[index];
                        Vec2 next = loop.Vertices[(index + 1) % count].Point;

                        if (Vec2.Distance(current.Point, next) <= 1e-12)
                        {
                            continue;
                        }

                        Vec2 incoming = ArcMath.EndTangent(previous.Point, current.Point, previous.Bulge);
                        Vec2 outgoing = ArcMath.StartTangent(current.Point, next, current.Bulge);

                        bool corner = Math.Abs(Vec2.Cross(incoming, outgoing)) > cornerSine ||
                                      Vec2.Dot(incoming, outgoing) < 0d;

                        bool silhouette = Math.Abs(Vec2.Dot(incoming, Sideways)) < parallel ||
                                          Math.Abs(Vec2.Dot(outgoing, Sideways)) < parallel;

                        if (corner || silhouette)
                        {
                            points.Add(current.Point);
                        }

                        if (ArcMath.TryDescribe(current.Point, next, current.Bulge,
                                out Vec2 centre, out double radius, out double startAngle, out double sweep))
                        {
                            foreach (Vec2 side in new[] { Sideways, Sideways * -1d })
                            {
                                double angle = Math.Atan2(side.Y, side.X);
                                if (ArcMath.IsWithinSweep(startAngle, sweep, angle, 1e-9))
                                {
                                    points.Add(centre + (side * radius));
                                }
                            }
                        }
                    }
                }

                return points;
            }

            /// <summary>
            /// Whether material lies between a point on a section and the viewer. The test is made
            /// just either side of the point's line of sight, and only material found on both
            /// sides counts: a line of sight that merely grazes a face, such as one running down
            /// the face of a web, is not blocked by it.
            /// </summary>
            public bool IsOccluded(Vec2 point, IReadOnlyList<PartData> occluders)
            {
                double depth = Vec2.Dot(point, Toward);
                double across = Across(point);

                List<(double Low, double High)> left = Union(occluders.SelectMany(p => InsideIntervals(p, across - Offset)));
                List<(double Low, double High)> right = Union(occluders.SelectMany(p => InsideIntervals(p, across + Offset)));

                foreach ((double low, double high) in Intersect(left, right))
                {
                    double from = Math.Max(low, depth);
                    if (high > depth + OcclusionTolerance && high - from > OcclusionTolerance)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>Whether a point lies strictly inside a part's material, clear of its outline.</summary>
            public bool IsInterior(Vec2 point, PartData part)
            {
                bool inside = false;
                double clearance = double.MaxValue;

                foreach (List<Vec2> loop in part.FlatLoops)
                {
                    int count = loop.Count;
                    for (int index = 0; index < count; index++)
                    {
                        Vec2 a = loop[index];
                        Vec2 b = loop[(index + 1) % count];

                        if ((a.Y > point.Y) != (b.Y > point.Y))
                        {
                            double x = a.X + ((point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                            if (x > point.X)
                            {
                                inside = !inside;
                            }
                        }

                        clearance = Math.Min(clearance, DistanceToSegment(point, a, b));
                    }
                }

                return inside && clearance > Offset * 10d;
            }

            private IEnumerable<(double Low, double High)> InsideIntervals(PartData part, double across)
            {
                List<double> crossings = new();

                foreach (List<Vec2> loop in part.FlatLoops)
                {
                    int count = loop.Count;
                    for (int index = 0; index < count; index++)
                    {
                        Vec2 a = loop[index];
                        Vec2 b = loop[(index + 1) % count];
                        double ta = Across(a);
                        double tb = Across(b);

                        if ((ta < across) != (tb < across))
                        {
                            double fraction = (across - ta) / (tb - ta);
                            Vec2 hit = a + ((b - a) * fraction);
                            crossings.Add(Vec2.Dot(hit, Toward));
                        }
                    }
                }

                crossings.Sort();

                for (int index = 0; index + 1 < crossings.Count; index += 2)
                {
                    yield return (crossings[index], crossings[index + 1]);
                }
            }

            private static List<(double Low, double High)> Union(IEnumerable<(double Low, double High)> intervals)
            {
                List<(double Low, double High)> result = new();

                foreach ((double low, double high) in intervals.OrderBy(i => i.Low))
                {
                    if (result.Count > 0 && low <= result[result.Count - 1].High)
                    {
                        (double lastLow, double lastHigh) = result[result.Count - 1];
                        result[result.Count - 1] = (lastLow, Math.Max(lastHigh, high));
                    }
                    else
                    {
                        result.Add((low, high));
                    }
                }

                return result;
            }

            private static IEnumerable<(double Low, double High)> Intersect(
                List<(double Low, double High)> left, List<(double Low, double High)> right)
            {
                foreach ((double leftLow, double leftHigh) in left)
                {
                    foreach ((double rightLow, double rightHigh) in right)
                    {
                        double low = Math.Max(leftLow, rightLow);
                        double high = Math.Min(leftHigh, rightHigh);

                        if (high > low)
                        {
                            yield return (low, high);
                        }
                    }
                }
            }

            private static double DistanceToSegment(Vec2 point, Vec2 a, Vec2 b)
            {
                Vec2 edge = b - a;
                double lengthSquared = Vec2.Dot(edge, edge);

                if (lengthSquared <= 1e-24)
                {
                    return Vec2.Distance(point, a);
                }

                double t = Math.Max(0d, Math.Min(1d, Vec2.Dot(point - a, edge) / lengthSquared));
                return Vec2.Distance(point, a + (edge * t));
            }
        }
    }
}
