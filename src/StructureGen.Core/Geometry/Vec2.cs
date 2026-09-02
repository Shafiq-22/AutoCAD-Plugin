using System;
using System.Globalization;

namespace StructureGen.Core.Geometry
{
    /// <summary>A two-dimensional point in the local profile plane, in millimetres.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        /// <summary>Initialises a new instance of the <see cref="Vec2"/> struct.</summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Gets the origin.</summary>
        public static Vec2 Zero => new(0d, 0d);

        /// <summary>Gets the X component.</summary>
        public double X { get; }

        /// <summary>Gets the Y component.</summary>
        public double Y { get; }

        /// <summary>Gets the euclidean length of the vector.</summary>
        public double Magnitude => Math.Sqrt((X * X) + (Y * Y));

        /// <summary>Adds two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The sum.</returns>
        public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);

        /// <summary>Subtracts one vector from another.</summary>
        /// <param name="left">The vector to subtract from.</param>
        /// <param name="right">The vector to subtract.</param>
        /// <returns>The difference.</returns>
        public static Vec2 operator -(Vec2 left, Vec2 right) => new(left.X - right.X, left.Y - right.Y);

        /// <summary>Scales a vector.</summary>
        /// <param name="vector">The vector.</param>
        /// <param name="factor">The scale factor.</param>
        /// <returns>The scaled vector.</returns>
        public static Vec2 operator *(Vec2 vector, double factor) => new(vector.X * factor, vector.Y * factor);

        /// <summary>Determines whether two vectors are exactly equal.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns><see langword="true"/> when the vectors are equal.</returns>
        public static bool operator ==(Vec2 left, Vec2 right) => left.Equals(right);

        /// <summary>Determines whether two vectors differ.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns><see langword="true"/> when the vectors differ.</returns>
        public static bool operator !=(Vec2 left, Vec2 right) => !left.Equals(right);

        /// <summary>Returns the distance between two points.</summary>
        /// <param name="from">The first point.</param>
        /// <param name="to">The second point.</param>
        /// <returns>The distance.</returns>
        public static double Distance(Vec2 from, Vec2 to) => (to - from).Magnitude;

        /// <summary>Returns the Z component of the cross product of two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The cross product's Z component, whose sign gives the turn direction.</returns>
        public static double Cross(Vec2 left, Vec2 right) => (left.X * right.Y) - (left.Y * right.X);

        /// <summary>Returns the dot product of two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The dot product.</returns>
        public static double Dot(Vec2 left, Vec2 right) => (left.X * right.X) + (left.Y * right.Y);

        /// <summary>Returns a unit vector pointing the same way as this one.</summary>
        /// <param name="tolerance">Magnitudes at or below this are treated as degenerate.</param>
        /// <returns>The normalised vector.</returns>
        /// <exception cref="InvalidOperationException">The vector is too short to normalise.</exception>
        public Vec2 Normalised(double tolerance = 1e-12)
        {
            double magnitude = Magnitude;
            if (magnitude <= tolerance)
            {
                throw new InvalidOperationException("A zero-length vector has no direction and cannot be normalised.");
            }

            return new Vec2(X / magnitude, Y / magnitude);
        }

        /// <summary>Determines whether this point coincides with another within a tolerance.</summary>
        /// <param name="other">The point to compare against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns><see langword="true"/> when the points are within tolerance of each other.</returns>
        public bool IsEqualTo(Vec2 other, double tolerance = 1e-9) =>
            Math.Abs(X - other.X) <= tolerance && Math.Abs(Y - other.Y) <= tolerance;

        /// <inheritdoc />
        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(X, Y);

        /// <inheritdoc />
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####})", X, Y);
    }
}
