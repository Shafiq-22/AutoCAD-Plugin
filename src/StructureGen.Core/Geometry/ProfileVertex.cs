using System;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// One vertex of a profile loop, carrying the bulge of the segment that leaves it.
    /// </summary>
    /// <remarks>
    /// The bulge is the tangent of a quarter of the arc's included angle, which is exactly the
    /// representation an AutoCAD lightweight polyline uses. Storing profiles this way means the
    /// conversion in the AutoCAD layer is a direct copy with no re-derivation, so a fillet computed
    /// and unit tested off-line is the same fillet that ends up in the drawing. A bulge of zero is
    /// a straight segment; a positive bulge turns anticlockwise.
    /// </remarks>
    public readonly struct ProfileVertex : IEquatable<ProfileVertex>
    {
        /// <summary>Initialises a new instance of the <see cref="ProfileVertex"/> struct.</summary>
        /// <param name="point">The vertex position.</param>
        /// <param name="bulge">The bulge of the segment leaving this vertex.</param>
        public ProfileVertex(Vec2 point, double bulge = 0d)
        {
            Point = point;
            Bulge = bulge;
        }

        /// <summary>Initialises a new instance of the <see cref="ProfileVertex"/> struct.</summary>
        /// <param name="x">The X position.</param>
        /// <param name="y">The Y position.</param>
        /// <param name="bulge">The bulge of the segment leaving this vertex.</param>
        public ProfileVertex(double x, double y, double bulge = 0d)
            : this(new Vec2(x, y), bulge)
        {
        }

        /// <summary>Gets the vertex position.</summary>
        public Vec2 Point { get; }

        /// <summary>Gets the bulge of the segment leaving this vertex.</summary>
        public double Bulge { get; }

        /// <summary>Gets a value indicating whether the segment leaving this vertex is an arc.</summary>
        public bool IsArc => Math.Abs(Bulge) > 1e-12;

        /// <summary>Returns a copy of this vertex with a different bulge.</summary>
        /// <param name="bulge">The replacement bulge.</param>
        /// <returns>The new vertex.</returns>
        public ProfileVertex WithBulge(double bulge) => new(Point, bulge);

        /// <summary>Returns a copy of this vertex moved by an offset.</summary>
        /// <param name="offset">The offset to apply.</param>
        /// <returns>The new vertex.</returns>
        public ProfileVertex Translated(Vec2 offset) => new(Point + offset, Bulge);

        /// <inheritdoc />
        public bool Equals(ProfileVertex other) => Point.Equals(other.Point) && Bulge.Equals(other.Bulge);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ProfileVertex other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Point, Bulge);

        /// <inheritdoc />
        public override string ToString() => IsArc ? $"{Point} bulge {Bulge:0.#####}" : Point.ToString();
    }
}
