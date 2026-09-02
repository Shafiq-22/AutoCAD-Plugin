using System;
using System.Collections.Generic;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// Builds a closed profile loop from corner points, resolving requested corner radii into
    /// tangent points and bulges.
    /// </summary>
    /// <remarks>
    /// Every rolled section is described here as the polygon a draughtsman would sketch, plus a
    /// radius at the corners that the rolling process actually rounds. One general routine turns
    /// that into arcs, so a root fillet on an I section, the toe radius of an angle and the outside
    /// corner of a hollow section all go through the same tested code path instead of each
    /// generator hand-rolling its own trigonometry.
    ///
    /// A radius that will not fit is reduced to the largest one that does rather than producing
    /// self-intersecting geometry, and the reduction is reported through
    /// <see cref="ClampedCornerCount"/> so a database with an implausible radius can be flagged.
    /// </remarks>
    public sealed class ProfileLoopBuilder
    {
        private readonly List<(Vec2 Point, double Radius)> _corners = new();

        /// <summary>Gets the number of corners whose requested radius had to be reduced to fit.</summary>
        public int ClampedCornerCount { get; private set; }

        /// <summary>Adds a corner.</summary>
        /// <param name="x">The X position.</param>
        /// <param name="y">The Y position.</param>
        /// <param name="radius">The corner radius. Zero or less produces a sharp corner.</param>
        /// <returns>This builder, so calls can be chained.</returns>
        public ProfileLoopBuilder Add(double x, double y, double radius = 0d) => Add(new Vec2(x, y), radius);

        /// <summary>Adds a corner.</summary>
        /// <param name="point">The corner position.</param>
        /// <param name="radius">The corner radius. Zero or less produces a sharp corner.</param>
        /// <returns>This builder, so calls can be chained.</returns>
        public ProfileLoopBuilder Add(Vec2 point, double radius = 0d)
        {
            _corners.Add((point, Math.Max(0d, radius)));
            return this;
        }

        /// <summary>Builds the loop.</summary>
        /// <param name="isOuter">Whether the contour bounds material rather than a void.</param>
        /// <param name="canonicalWinding">
        /// When <see langword="true"/> the result is wound anticlockwise for an outer contour and
        /// clockwise for a void, which is the convention the geometry layer relies on.
        /// </param>
        /// <returns>The closed loop.</returns>
        /// <exception cref="InvalidOperationException">Fewer than three corners were supplied.</exception>
        public ProfileLoop Build(bool isOuter = true, bool canonicalWinding = true)
        {
            if (_corners.Count < 3)
            {
                throw new InvalidOperationException(
                    $"A profile loop needs at least three corners; {_corners.Count} were supplied.");
            }

            ClampedCornerCount = 0;
            List<ProfileVertex> vertices = new(_corners.Count * 2);
            int count = _corners.Count;

            for (int index = 0; index < count; index++)
            {
                (Vec2 corner, double radius) = _corners[index];
                Vec2 previous = _corners[(index - 1 + count) % count].Point;
                Vec2 next = _corners[(index + 1) % count].Point;

                if (radius <= 0d)
                {
                    vertices.Add(new ProfileVertex(corner));
                    continue;
                }

                if (!TryRoundCorner(previous, corner, next, radius, out ProfileVertex start, out ProfileVertex end, out bool clamped))
                {
                    vertices.Add(new ProfileVertex(corner));
                    continue;
                }

                if (clamped)
                {
                    ClampedCornerCount++;
                }

                vertices.Add(start);
                vertices.Add(end);
            }

            ProfileLoop loop = new(vertices, isOuter);
            return canonicalWinding ? loop.WithCanonicalWinding() : loop;
        }

        /// <summary>
        /// Computes the tangent points and bulge of a fillet at one corner.
        /// </summary>
        /// <param name="previous">The preceding corner.</param>
        /// <param name="corner">The corner to round.</param>
        /// <param name="next">The following corner.</param>
        /// <param name="radius">The requested radius.</param>
        /// <param name="start">Receives the tangent point on the incoming edge, carrying the arc's bulge.</param>
        /// <param name="end">Receives the tangent point on the outgoing edge.</param>
        /// <param name="clamped">Receives whether the radius had to be reduced to fit.</param>
        /// <returns><see langword="false"/> when the corner is degenerate and cannot be rounded.</returns>
        public static bool TryRoundCorner(
            Vec2 previous,
            Vec2 corner,
            Vec2 next,
            double radius,
            out ProfileVertex start,
            out ProfileVertex end,
            out bool clamped)
        {
            start = new ProfileVertex(corner);
            end = new ProfileVertex(corner);
            clamped = false;

            Vec2 incoming = previous - corner;
            Vec2 outgoing = next - corner;
            double incomingLength = incoming.Magnitude;
            double outgoingLength = outgoing.Magnitude;

            if (incomingLength <= 1e-9 || outgoingLength <= 1e-9 || radius <= 0d)
            {
                return false;
            }

            Vec2 incomingUnit = incoming * (1d / incomingLength);
            Vec2 outgoingUnit = outgoing * (1d / outgoingLength);

            double cosine = Math.Clamp(Vec2.Dot(incomingUnit, outgoingUnit), -1d, 1d);
            double interiorAngle = Math.Acos(cosine);

            // A corner that doubles back on itself or does not turn at all cannot be filleted.
            if (interiorAngle <= 1e-6 || Math.Abs(interiorAngle - Math.PI) <= 1e-6)
            {
                return false;
            }

            double halfAngleTangent = Math.Tan(interiorAngle / 2d);
            if (halfAngleTangent <= 1e-9)
            {
                return false;
            }

            double tangentDistance = radius / halfAngleTangent;

            // Each fillet may consume at most half of the edges it sits between, so that a radius
            // at both ends of a short edge cannot overlap.
            double maximumDistance = Math.Min(incomingLength, outgoingLength) * 0.5d;
            if (tangentDistance > maximumDistance)
            {
                tangentDistance = maximumDistance;
                radius = tangentDistance * halfAngleTangent;
                clamped = true;

                if (radius <= 1e-9)
                {
                    return false;
                }
            }

            Vec2 startPoint = corner + (incomingUnit * tangentDistance);
            Vec2 endPoint = corner + (outgoingUnit * tangentDistance);

            double turn = Vec2.Cross(corner - previous, next - corner);
            double sweep = Math.PI - interiorAngle;
            double signedSweep = turn >= 0d ? sweep : -sweep;

            start = new ProfileVertex(startPoint, Math.Tan(signedSweep / 4d));
            end = new ProfileVertex(endPoint);
            return true;
        }

        /// <summary>Builds a closed loop approximating a full circle.</summary>
        /// <param name="centre">The centre.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="isOuter">Whether the circle bounds material rather than a void.</param>
        /// <returns>
        /// A two-vertex loop of two exact half-circle arcs, which is how AutoCAD itself represents a
        /// circle as a polyline. The result is a true circle, not a polygonal approximation.
        /// </returns>
        public static ProfileLoop Circle(Vec2 centre, double radius, bool isOuter = true)
        {
            if (radius <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "A circle needs a radius greater than zero.");
            }

            // A bulge of one is a half turn: tan(180 deg / 4) = tan(45 deg) = 1.
            double bulge = isOuter ? 1d : -1d;
            Vec2 left = new(centre.X - radius, centre.Y);
            Vec2 right = new(centre.X + radius, centre.Y);

            return isOuter
                ? new ProfileLoop(new[] { new ProfileVertex(left, bulge), new ProfileVertex(right, bulge) }, true)
                : new ProfileLoop(new[] { new ProfileVertex(right, bulge), new ProfileVertex(left, bulge) }, false);
        }

        /// <summary>Builds a rectangle centred on the origin, optionally with rounded corners.</summary>
        /// <param name="width">The width along X.</param>
        /// <param name="height">The height along Y.</param>
        /// <param name="cornerRadius">The corner radius. Zero produces sharp corners.</param>
        /// <param name="isOuter">Whether the rectangle bounds material rather than a void.</param>
        /// <returns>The loop.</returns>
        public static ProfileLoop CentredRectangle(double width, double height, double cornerRadius = 0d, bool isOuter = true)
        {
            if (width <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A rectangle needs a width greater than zero.");
            }

            if (height <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "A rectangle needs a height greater than zero.");
            }

            double halfWidth = width / 2d;
            double halfHeight = height / 2d;

            ProfileLoopBuilder builder = new();
            builder.Add(-halfWidth, -halfHeight, cornerRadius)
                   .Add(halfWidth, -halfHeight, cornerRadius)
                   .Add(halfWidth, halfHeight, cornerRadius)
                   .Add(-halfWidth, halfHeight, cornerRadius);

            return builder.Build(isOuter);
        }

        /// <summary>Builds a regular polygon defined by its across-flats distance.</summary>
        /// <param name="centre">The centre.</param>
        /// <param name="acrossFlats">The distance between opposite flats.</param>
        /// <param name="sides">The number of sides. Must be even for across-flats to be meaningful.</param>
        /// <param name="isOuter">Whether the polygon bounds material rather than a void.</param>
        /// <returns>The loop.</returns>
        /// <remarks>
        /// Fastener standards dimension hexagon heads and nuts across the flats, so that is the
        /// input taken here rather than a circumscribed radius.
        /// </remarks>
        public static ProfileLoop RegularPolygonAcrossFlats(Vec2 centre, double acrossFlats, int sides = 6, bool isOuter = true)
        {
            if (sides < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(sides), sides, "A polygon needs at least three sides.");
            }

            if (acrossFlats <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(acrossFlats), acrossFlats, "The across-flats distance must be greater than zero.");
            }

            double apothem = acrossFlats / 2d;
            double circumradius = apothem / Math.Cos(Math.PI / sides);
            double startAngle = Math.PI / sides;

            List<ProfileVertex> vertices = new(sides);
            for (int index = 0; index < sides; index++)
            {
                double angle = startAngle + (2d * Math.PI * index / sides);
                vertices.Add(new ProfileVertex(
                    centre.X + (circumradius * Math.Cos(angle)),
                    centre.Y + (circumradius * Math.Sin(angle))));
            }

            return new ProfileLoop(vertices, isOuter).WithCanonicalWinding();
        }
    }
}
