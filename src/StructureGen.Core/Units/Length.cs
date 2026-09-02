using System;
using System.Globalization;

namespace StructureGen.Core.Units
{
    /// <summary>
    /// A length, stored canonically in millimetres as a <see cref="decimal"/>.
    /// </summary>
    /// <remarks>
    /// Using a dedicated type instead of a bare number removes the most common source of
    /// error in a mixed metric/imperial CAD tool: a value whose unit is implied by a
    /// variable name rather than by the type system.
    /// </remarks>
    public readonly struct Length : IEquatable<Length>, IComparable<Length>
    {
        private Length(decimal millimetres) => Millimetres = millimetres;

        /// <summary>Gets a zero length.</summary>
        public static Length Zero => new(0m);

        /// <summary>Gets the value of this length in millimetres.</summary>
        public decimal Millimetres { get; }

        /// <summary>Gets the value of this length in metres.</summary>
        public decimal Metres => UnitConverter.FromMillimetres(Millimetres, LengthUnit.Metre);

        /// <summary>Gets a value indicating whether the length is greater than zero.</summary>
        public bool IsPositive => Millimetres > 0m;

        /// <summary>Creates a length from a value in millimetres.</summary>
        /// <param name="millimetres">The value in millimetres.</param>
        /// <returns>The length.</returns>
        public static Length FromMillimetres(decimal millimetres) => new(millimetres);

        /// <summary>Creates a length from a value in metres.</summary>
        /// <param name="metres">The value in metres.</param>
        /// <returns>The length.</returns>
        public static Length FromMetres(decimal metres) => new(UnitConverter.ToMillimetres(metres, LengthUnit.Metre));

        /// <summary>Creates a length from a value expressed in an arbitrary supported unit.</summary>
        /// <param name="value">The value.</param>
        /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
        /// <returns>The length.</returns>
        public static Length From(decimal value, LengthUnit unit) => new(UnitConverter.ToMillimetres(value, unit));

        /// <summary>Expresses this length in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted value.</returns>
        public decimal To(LengthUnit unit) => UnitConverter.FromMillimetres(Millimetres, unit);

        /// <summary>Adds two lengths.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns>The sum.</returns>
        public static Length operator +(Length left, Length right) => new(left.Millimetres + right.Millimetres);

        /// <summary>Subtracts one length from another.</summary>
        /// <param name="left">The length to subtract from.</param>
        /// <param name="right">The length to subtract.</param>
        /// <returns>The difference.</returns>
        public static Length operator -(Length left, Length right) => new(left.Millimetres - right.Millimetres);

        /// <summary>Scales a length by a factor.</summary>
        /// <param name="length">The length.</param>
        /// <param name="factor">The factor.</param>
        /// <returns>The scaled length.</returns>
        public static Length operator *(Length length, decimal factor) => new(length.Millimetres * factor);

        /// <summary>Determines whether two lengths are equal.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when the lengths are equal.</returns>
        public static bool operator ==(Length left, Length right) => left.Equals(right);

        /// <summary>Determines whether two lengths differ.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when the lengths differ.</returns>
        public static bool operator !=(Length left, Length right) => !left.Equals(right);

        /// <summary>Determines whether one length is shorter than another.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when <paramref name="left"/> is shorter.</returns>
        public static bool operator <(Length left, Length right) => left.Millimetres < right.Millimetres;

        /// <summary>Determines whether one length is longer than another.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when <paramref name="left"/> is longer.</returns>
        public static bool operator >(Length left, Length right) => left.Millimetres > right.Millimetres;

        /// <inheritdoc />
        public bool Equals(Length other) => Millimetres == other.Millimetres;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Length other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Millimetres.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(Length other) => Millimetres.CompareTo(other.Millimetres);

        /// <inheritdoc />
        public override string ToString() =>
            Millimetres.ToString("0.###", CultureInfo.InvariantCulture) + " mm";
    }
}
