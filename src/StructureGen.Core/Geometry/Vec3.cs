using System;
using System.Globalization;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// A three-dimensional vector in drawing millimetres.
    /// </summary>
    /// <remarks>
    /// StructureGen deliberately defines its own vector type rather than using
    /// <c>Autodesk.AutoCAD.Geometry.Point3d</c> in the shared layers. Keeping the AutoCAD types
    /// out of the core is what allows placement maths, profile generation and the bill of
    /// materials to be unit tested on a machine with no AutoCAD installed.
    /// </remarks>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>Initialises a new instance of the <see cref="Vec3"/> struct.</summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Gets the origin.</summary>
        public static Vec3 Origin => new(0d, 0d, 0d);

        /// <summary>Gets the unit vector along X.</summary>
        public static Vec3 UnitX => new(1d, 0d, 0d);

        /// <summary>Gets the unit vector along Y.</summary>
        public static Vec3 UnitY => new(0d, 1d, 0d);

        /// <summary>Gets the unit vector along Z.</summary>
        public static Vec3 UnitZ => new(0d, 0d, 1d);

        /// <summary>Gets the X component.</summary>
        public double X { get; }

        /// <summary>Gets the Y component.</summary>
        public double Y { get; }

        /// <summary>Gets the Z component.</summary>
        public double Z { get; }

        /// <summary>Gets the euclidean length of the vector.</summary>
        public double Magnitude => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        /// <summary>Adds two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The sum.</returns>
        public static Vec3 operator +(Vec3 left, Vec3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        /// <summary>Subtracts one vector from another.</summary>
        /// <param name="left">The vector to subtract from.</param>
        /// <param name="right">The vector to subtract.</param>
        /// <returns>The difference.</returns>
        public static Vec3 operator -(Vec3 left, Vec3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        /// <summary>Scales a vector.</summary>
        /// <param name="vector">The vector.</param>
        /// <param name="factor">The scale factor.</param>
        /// <returns>The scaled vector.</returns>
        public static Vec3 operator *(Vec3 vector, double factor) => new(vector.X * factor, vector.Y * factor, vector.Z * factor);

        /// <summary>Determines whether two vectors are exactly equal.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns><see langword="true"/> when the vectors are equal.</returns>
        public static bool operator ==(Vec3 left, Vec3 right) => left.Equals(right);

        /// <summary>Determines whether two vectors differ.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns><see langword="true"/> when the vectors differ.</returns>
        public static bool operator !=(Vec3 left, Vec3 right) => !left.Equals(right);

        /// <summary>Returns the distance between two points.</summary>
        /// <param name="from">The first point.</param>
        /// <param name="to">The second point.</param>
        /// <returns>The distance.</returns>
        public static double Distance(Vec3 from, Vec3 to) => (to - from).Magnitude;

        /// <summary>Returns the dot product of two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The dot product.</returns>
        public static double Dot(Vec3 left, Vec3 right) => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

        /// <summary>Returns the cross product of two vectors.</summary>
        /// <param name="left">The first vector.</param>
        /// <param name="right">The second vector.</param>
        /// <returns>The cross product.</returns>
        public static Vec3 Cross(Vec3 left, Vec3 right) => new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

        /// <summary>Returns a unit vector pointing the same way as this one.</summary>
        /// <param name="tolerance">Magnitudes at or below this are treated as degenerate.</param>
        /// <returns>The normalised vector.</returns>
        /// <exception cref="InvalidOperationException">The vector is too short to normalise.</exception>
        public Vec3 Normalised(double tolerance = 1e-9)
        {
            double magnitude = Magnitude;
            if (magnitude <= tolerance)
            {
                throw new InvalidOperationException("A zero-length vector has no direction and cannot be normalised.");
            }

            return new Vec3(X / magnitude, Y / magnitude, Z / magnitude);
        }

        /// <summary>Determines whether this vector equals another within a tolerance.</summary>
        /// <param name="other">The vector to compare against.</param>
        /// <param name="tolerance">The per-component tolerance.</param>
        /// <returns><see langword="true"/> when every component is within tolerance.</returns>
        public bool IsEqualTo(Vec3 other, double tolerance = 1e-9) =>
            Math.Abs(X - other.X) <= tolerance &&
            Math.Abs(Y - other.Y) <= tolerance &&
            Math.Abs(Z - other.Z) <= tolerance;

        /// <inheritdoc />
        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Vec3 other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        /// <inheritdoc />
        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", X, Y, Z);
    }
}
