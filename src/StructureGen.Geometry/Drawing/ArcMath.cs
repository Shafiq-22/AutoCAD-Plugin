using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;

namespace StructureGen.Geometry.Drawing
{
    /// <summary>The arithmetic of bulge arcs that the plan projection needs.</summary>
    internal static class ArcMath
    {
        /// <summary>Describes the arc leaving a vertex.</summary>
        /// <param name="start">The segment start.</param>
        /// <param name="end">The segment end.</param>
        /// <param name="bulge">The bulge; positive turns anticlockwise.</param>
        /// <param name="centre">The arc centre.</param>
        /// <param name="radius">The arc radius.</param>
        /// <param name="startAngle">The angle of the start point about the centre, in radians.</param>
        /// <param name="sweep">The signed included angle, in radians.</param>
        /// <returns><see langword="false"/> when the segment is straight or degenerate.</returns>
        public static bool TryDescribe(
            Vec2 start, Vec2 end, double bulge,
            out Vec2 centre, out double radius, out double startAngle, out double sweep)
        {
            centre = Vec2.Zero;
            radius = 0d;
            startAngle = 0d;
            sweep = 0d;

            Vec2 chord = end - start;
            double length = chord.Magnitude;

            if (Math.Abs(bulge) <= 1e-12 || length <= 1e-12)
            {
                return false;
            }

            sweep = 4d * Math.Atan(bulge);

            // The centre sits on the chord's perpendicular bisector; its distance from the chord is
            // half the chord times (1 - b^2) / 2b, on the left for an anticlockwise arc.
            Vec2 direction = chord * (1d / length);
            Vec2 left = new(-direction.Y, direction.X);
            Vec2 middle = new((start.X + end.X) / 2d, (start.Y + end.Y) / 2d);
            double offset = (length / 2d) * ((1d - (bulge * bulge)) / (2d * bulge));

            centre = middle + (left * offset);
            radius = Vec2.Distance(centre, start);
            startAngle = Math.Atan2(start.Y - centre.Y, start.X - centre.X);
            return true;
        }

        /// <summary>The unit tangent where a segment leaves its start.</summary>
        public static Vec2 StartTangent(Vec2 start, Vec2 end, double bulge)
        {
            Vec2 chord = (end - start).Normalised();
            double half = 2d * Math.Atan(bulge);
            return Rotate(chord, -half);
        }

        /// <summary>The unit tangent where a segment arrives at its end.</summary>
        public static Vec2 EndTangent(Vec2 start, Vec2 end, double bulge)
        {
            Vec2 chord = (end - start).Normalised();
            double half = 2d * Math.Atan(bulge);
            return Rotate(chord, half);
        }

        /// <summary>The length of one segment, straight or arc.</summary>
        public static double SegmentLength(Vec2 start, Vec2 end, double bulge)
        {
            if (!TryDescribe(start, end, bulge, out _, out double radius, out _, out double sweep))
            {
                return Vec2.Distance(start, end);
            }

            return Math.Abs(sweep) * radius;
        }

        /// <summary>
        /// Replaces arcs by chords no more than a given angle apart, for the tests that do not need
        /// exact arcs.
        /// </summary>
        /// <param name="vertices">The vertices with bulges.</param>
        /// <param name="closed">Whether the run closes.</param>
        /// <param name="stepDegrees">The largest angle one chord may span.</param>
        /// <returns>The points, without repeating the first at the end.</returns>
        public static List<Vec2> Flatten(IReadOnlyList<ProfileVertex> vertices, bool closed, double stepDegrees)
        {
            List<Vec2> points = new();
            int count = vertices.Count;
            int segments = closed ? count : count - 1;

            for (int index = 0; index < segments; index++)
            {
                ProfileVertex start = vertices[index];
                Vec2 end = vertices[(index + 1) % count].Point;
                points.Add(start.Point);

                if (!TryDescribe(start.Point, end, start.Bulge, out Vec2 centre, out double radius, out double angle, out double sweep))
                {
                    continue;
                }

                int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (stepDegrees * Math.PI / 180d)));

                for (int step = 1; step < steps; step++)
                {
                    double at = angle + (sweep * step / steps);
                    points.Add(new Vec2(centre.X + (radius * Math.Cos(at)), centre.Y + (radius * Math.Sin(at))));
                }
            }

            if (!closed)
            {
                points.Add(vertices[count - 1].Point);
            }

            return points;
        }

        /// <summary>Whether an angle falls strictly inside an arc's sweep.</summary>
        public static bool IsWithinSweep(double startAngle, double sweep, double angle, double tolerance)
        {
            double relative = angle - startAngle;

            if (sweep >= 0d)
            {
                relative = Normalise(relative);
                return relative > tolerance && relative < sweep - tolerance;
            }

            relative = Normalise(-relative);
            return relative > tolerance && relative < -sweep - tolerance;
        }

        /// <summary>Rotates a vector anticlockwise.</summary>
        public static Vec2 Rotate(Vec2 vector, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vec2((vector.X * cos) - (vector.Y * sin), (vector.X * sin) + (vector.Y * cos));
        }

        private static double Normalise(double radians)
        {
            double full = 2d * Math.PI;
            double result = radians % full;
            return result < 0d ? result + full : result;
        }
    }
}
