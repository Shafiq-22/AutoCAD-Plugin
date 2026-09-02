using System;
using System.Globalization;

namespace StructureGen.Core.Units
{
    /// <summary>A mass, stored canonically in kilograms as a <see cref="decimal"/>.</summary>
    public readonly struct Mass : IEquatable<Mass>, IComparable<Mass>
    {
        private Mass(decimal kilograms) => Kilograms = kilograms;

        /// <summary>Gets a zero mass.</summary>
        public static Mass Zero => new(0m);

        /// <summary>Gets the value of this mass in kilograms.</summary>
        public decimal Kilograms { get; }

        /// <summary>Gets the value of this mass in metric tonnes.</summary>
        public decimal Tonnes => UnitConverter.FromKilograms(Kilograms, MassUnit.Tonne);

        /// <summary>Creates a mass from a value in kilograms.</summary>
        /// <param name="kilograms">The value in kilograms.</param>
        /// <returns>The mass.</returns>
        public static Mass FromKilograms(decimal kilograms) => new(kilograms);

        /// <summary>Creates a mass from a value expressed in an arbitrary supported unit.</summary>
        /// <param name="value">The value.</param>
        /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
        /// <returns>The mass.</returns>
        public static Mass From(decimal value, MassUnit unit) => new(UnitConverter.ToKilograms(value, unit));

        /// <summary>Expresses this mass in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted value.</returns>
        public decimal To(MassUnit unit) => UnitConverter.FromKilograms(Kilograms, unit);

        /// <summary>Adds two masses.</summary>
        /// <param name="left">The first mass.</param>
        /// <param name="right">The second mass.</param>
        /// <returns>The sum.</returns>
        public static Mass operator +(Mass left, Mass right) => new(left.Kilograms + right.Kilograms);

        /// <summary>Scales a mass by a factor.</summary>
        /// <param name="mass">The mass.</param>
        /// <param name="factor">The factor.</param>
        /// <returns>The scaled mass.</returns>
        public static Mass operator *(Mass mass, decimal factor) => new(mass.Kilograms * factor);

        /// <summary>Determines whether two masses are equal.</summary>
        /// <param name="left">The first mass.</param>
        /// <param name="right">The second mass.</param>
        /// <returns><see langword="true"/> when the masses are equal.</returns>
        public static bool operator ==(Mass left, Mass right) => left.Equals(right);

        /// <summary>Determines whether two masses differ.</summary>
        /// <param name="left">The first mass.</param>
        /// <param name="right">The second mass.</param>
        /// <returns><see langword="true"/> when the masses differ.</returns>
        public static bool operator !=(Mass left, Mass right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(Mass other) => Kilograms == other.Kilograms;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Mass other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Kilograms.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(Mass other) => Kilograms.CompareTo(other.Kilograms);

        /// <inheritdoc />
        public override string ToString() => Kilograms.ToString("0.###", CultureInfo.InvariantCulture) + " kg";
    }
}
