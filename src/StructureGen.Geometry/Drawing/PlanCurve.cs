using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;

namespace StructureGen.Geometry.Drawing
{
    /// <summary>What a line on a plan drawing stands for, which decides how it is drawn.</summary>
    public enum PlanLineKind
    {
        /// <summary>An edge seen from above: a continuous line.</summary>
        Visible = 0,

        /// <summary>An edge behind material: a dashed line.</summary>
        Hidden = 1,

        /// <summary>An axis of symmetry: a chain line.</summary>
        Centre = 2
    }

    /// <summary>
    /// One line of a plan drawing: a run of vertices in the plan plane, in millimetres, where each
    /// vertex carries the bulge of the segment that leaves it.
    /// </summary>
    /// <remarks>
    /// Bulges are kept because they are exactly what an AutoCAD lightweight polyline stores, so a
    /// round bar seen end on reaches the drawing as a true circle rather than a polygon.
    /// </remarks>
    public sealed class PlanCurve
    {
        /// <summary>Initialises a new instance of the <see cref="PlanCurve"/> class.</summary>
        /// <param name="kind">How the line is drawn.</param>
        /// <param name="vertices">At least two vertices.</param>
        /// <param name="closed">Whether the last vertex joins back to the first.</param>
        /// <param name="patternSizeMm">
        /// A characteristic size of the element the line belongs to, so dashes can be scaled to suit
        /// it rather than to the whole drawing.
        /// </param>
        public PlanCurve(PlanLineKind kind, IEnumerable<ProfileVertex> vertices, bool closed, double patternSizeMm)
        {
            Kind = kind;
            Vertices = (vertices ?? throw new ArgumentNullException(nameof(vertices))).ToList();
            Closed = closed;
            PatternSizeMm = patternSizeMm;

            if (Vertices.Count < 2)
            {
                throw new ArgumentException("A plan line needs at least two vertices.", nameof(vertices));
            }
        }

        /// <summary>Gets how the line is drawn.</summary>
        public PlanLineKind Kind { get; }

        /// <summary>Gets the vertices, in plan millimetres.</summary>
        public IReadOnlyList<ProfileVertex> Vertices { get; }

        /// <summary>Gets a value indicating whether the last vertex joins back to the first.</summary>
        public bool Closed { get; }

        /// <summary>Gets a characteristic size of the element the line belongs to, in millimetres.</summary>
        public double PatternSizeMm { get; }

        /// <summary>Creates a straight line between two points.</summary>
        /// <param name="kind">How the line is drawn.</param>
        /// <param name="from">The start.</param>
        /// <param name="to">The end.</param>
        /// <param name="patternSizeMm">The characteristic size for dash scaling.</param>
        /// <returns>The line.</returns>
        public static PlanCurve Line(PlanLineKind kind, Vec2 from, Vec2 to, double patternSizeMm) =>
            new(kind, new[] { new ProfileVertex(from), new ProfileVertex(to) }, false, patternSizeMm);

        /// <summary>Gets the length of the curve, including its arcs, in millimetres.</summary>
        public double Length
        {
            get
            {
                double total = 0d;
                int segments = Closed ? Vertices.Count : Vertices.Count - 1;

                for (int index = 0; index < segments; index++)
                {
                    ProfileVertex start = Vertices[index];
                    Vec2 end = Vertices[(index + 1) % Vertices.Count].Point;
                    total += ArcMath.SegmentLength(start.Point, end, start.Bulge);
                }

                return total;
            }
        }

        /// <summary>Gets the bounds of the curve's vertices and arc extremes.</summary>
        /// <returns>The minimum and maximum corners.</returns>
        public (Vec2 Minimum, Vec2 Maximum) Bounds()
        {
            double minimumX = double.MaxValue;
            double minimumY = double.MaxValue;
            double maximumX = double.MinValue;
            double maximumY = double.MinValue;

            foreach (Vec2 point in ArcMath.Flatten(Vertices, Closed, 10d))
            {
                minimumX = Math.Min(minimumX, point.X);
                minimumY = Math.Min(minimumY, point.Y);
                maximumX = Math.Max(maximumX, point.X);
                maximumY = Math.Max(maximumY, point.Y);
            }

            return (new Vec2(minimumX, minimumY), new Vec2(maximumX, maximumY));
        }

        /// <summary>Returns a copy moved by an offset.</summary>
        /// <param name="offset">The offset.</param>
        /// <returns>The moved curve.</returns>
        public PlanCurve Translated(Vec2 offset) =>
            new(Kind, Vertices.Select(v => v.Translated(offset)), Closed, PatternSizeMm);

        /// <inheritdoc />
        public override string ToString() =>
            $"{Kind} {(Closed ? "closed" : "open")} {string.Join(" -> ", Vertices)}";
    }
}
