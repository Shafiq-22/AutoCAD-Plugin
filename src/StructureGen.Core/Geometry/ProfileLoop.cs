using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Geometry
{
    /// <summary>A single closed contour of a cross-section.</summary>
    public sealed class ProfileLoop
    {
        /// <summary>Initialises a new instance of the <see cref="ProfileLoop"/> class.</summary>
        /// <param name="vertices">The vertices, in order. The loop is implicitly closed.</param>
        /// <param name="isOuter">Whether this contour bounds material rather than a void.</param>
        public ProfileLoop(IEnumerable<ProfileVertex> vertices, bool isOuter = true)
        {
            Vertices = (vertices ?? throw new ArgumentNullException(nameof(vertices))).ToList();
            IsOuter = isOuter;

            if (Vertices.Count < 2)
            {
                throw new ArgumentException("A closed profile loop needs at least two vertices.", nameof(vertices));
            }
        }

        /// <summary>Gets the vertices, in order. The loop is implicitly closed.</summary>
        public IReadOnlyList<ProfileVertex> Vertices { get; }

        /// <summary>Gets a value indicating whether this contour bounds material rather than a void.</summary>
        public bool IsOuter { get; }

        /// <summary>
        /// Gets the signed area in square millimetres, positive when the vertices run anticlockwise.
        /// </summary>
        /// <remarks>
        /// The straight part is the shoelace formula; each arc segment then contributes the area of
        /// its circular segment, signed by the direction of the bulge. That makes the area exact for
        /// filleted rolled sections rather than an approximation of the fillets by chords, which is
        /// what allows the computed area to be checked against the published area of a section as a
        /// test of the generated geometry.
        /// </remarks>
        public double SignedArea
        {
            get
            {
                double area = 0d;
                int count = Vertices.Count;

                for (int index = 0; index < count; index++)
                {
                    ProfileVertex current = Vertices[index];
                    ProfileVertex next = Vertices[(index + 1) % count];

                    area += (current.Point.X * next.Point.Y) - (next.Point.X * current.Point.Y);
                }

                area *= 0.5d;

                for (int index = 0; index < count; index++)
                {
                    ProfileVertex current = Vertices[index];
                    if (!current.IsArc)
                    {
                        continue;
                    }

                    ProfileVertex next = Vertices[(index + 1) % count];
                    area += CircularSegmentArea(current.Point, next.Point, current.Bulge);
                }

                return area;
            }
        }

        /// <summary>Gets the unsigned area in square millimetres.</summary>
        public double Area => Math.Abs(SignedArea);

        /// <summary>Gets a value indicating whether the vertices run anticlockwise.</summary>
        public bool IsAnticlockwise => SignedArea > 0d;

        /// <summary>Returns a copy of this loop with the vertex order reversed.</summary>
        /// <returns>The reversed loop.</returns>
        /// <remarks>
        /// Reversing moves each bulge onto the preceding vertex and negates it, because a bulge
        /// belongs to the segment that leaves a vertex and the direction of travel has flipped.
        /// </remarks>
        public ProfileLoop Reversed()
        {
            int count = Vertices.Count;
            List<ProfileVertex> reversed = new(count);

            for (int index = count - 1; index >= 0; index--)
            {
                // Travelling backwards, the segment leaving vertex i is the old segment that
                // arrived at it, that is the segment that left vertex i-1.
                int previous = (index - 1 + count) % count;
                reversed.Add(new ProfileVertex(Vertices[index].Point, -Vertices[previous].Bulge));
            }

            return new ProfileLoop(reversed, IsOuter);
        }

        /// <summary>Returns a copy of this loop wound anticlockwise for an outer loop, clockwise for a void.</summary>
        /// <returns>The correctly wound loop.</returns>
        public ProfileLoop WithCanonicalWinding()
        {
            bool wantAnticlockwise = IsOuter;
            return IsAnticlockwise == wantAnticlockwise ? this : Reversed();
        }

        /// <summary>Returns a copy of this loop moved by an offset.</summary>
        /// <param name="offset">The offset to apply.</param>
        /// <returns>The translated loop.</returns>
        public ProfileLoop Translated(Vec2 offset) =>
            new(Vertices.Select(v => v.Translated(offset)), IsOuter);

        /// <summary>Gets the axis-aligned bounding box of the loop's vertices.</summary>
        /// <remarks>
        /// The box spans the vertices only. For a loop whose arcs bulge outwards beyond their chord
        /// end points the true extent is marginally larger; for the rolled sections generated here
        /// every arc is a fillet lying inside the vertex hull, so the two agree.
        /// </remarks>
        /// <returns>The minimum and maximum corners.</returns>
        public (Vec2 Minimum, Vec2 Maximum) Bounds()
        {
            double minimumX = Vertices.Min(v => v.Point.X);
            double minimumY = Vertices.Min(v => v.Point.Y);
            double maximumX = Vertices.Max(v => v.Point.X);
            double maximumY = Vertices.Max(v => v.Point.Y);
            return (new Vec2(minimumX, minimumY), new Vec2(maximumX, maximumY));
        }

        /// <summary>Computes the area between an arc and its chord.</summary>
        /// <param name="start">The chord start.</param>
        /// <param name="end">The chord end.</param>
        /// <param name="bulge">The bulge of the arc.</param>
        /// <returns>The signed segment area.</returns>
        public static double CircularSegmentArea(Vec2 start, Vec2 end, double bulge)
        {
            double chord = Vec2.Distance(start, end);
            if (chord <= 1e-12 || Math.Abs(bulge) <= 1e-12)
            {
                return 0d;
            }

            double includedAngle = 4d * Math.Atan(bulge);
            double halfAngleSine = Math.Sin(includedAngle / 2d);
            if (Math.Abs(halfAngleSine) <= 1e-12)
            {
                return 0d;
            }

            double radius = chord / (2d * halfAngleSine);
            return radius * radius * (includedAngle - Math.Sin(includedAngle)) / 2d;
        }
    }
}
