using System;
using System.Collections.Generic;

namespace StructureGen.Core.Units
{
    /// <summary>
    /// Exact conversions between the supported units and the canonical internal units
    /// (millimetres for length, kilograms for mass).
    /// </summary>
    /// <remarks>
    /// All factors are exact by definition and are held as <see cref="decimal"/> so that
    /// round-tripping a value through a conversion never introduces binary floating point
    /// drift. This matters because the same numbers end up in the bill of materials.
    /// </remarks>
    public static class UnitConverter
    {
        private static readonly Dictionary<LengthUnit, decimal> MillimetresPerLengthUnit = new()
        {
            [LengthUnit.Millimetre] = 1m,
            [LengthUnit.Centimetre] = 10m,
            [LengthUnit.Metre] = 1000m,
            [LengthUnit.Inch] = 25.4m,
            [LengthUnit.Foot] = 304.8m
        };

        private static readonly Dictionary<MassUnit, decimal> KilogramsPerMassUnit = new()
        {
            [MassUnit.Gram] = 0.001m,
            [MassUnit.Kilogram] = 1m,
            [MassUnit.Tonne] = 1000m,
            [MassUnit.Pound] = 0.45359237m
        };

        private static readonly Dictionary<LengthUnit, string> LengthAbbreviations = new()
        {
            [LengthUnit.Millimetre] = "mm",
            [LengthUnit.Centimetre] = "cm",
            [LengthUnit.Metre] = "m",
            [LengthUnit.Inch] = "in",
            [LengthUnit.Foot] = "ft"
        };

        private static readonly Dictionary<MassUnit, string> MassAbbreviations = new()
        {
            [MassUnit.Gram] = "g",
            [MassUnit.Kilogram] = "kg",
            [MassUnit.Tonne] = "t",
            [MassUnit.Pound] = "lb"
        };

        /// <summary>Converts a length expressed in <paramref name="unit"/> into millimetres.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
        /// <returns>The equivalent length in millimetres.</returns>
        public static decimal ToMillimetres(decimal value, LengthUnit unit) => value * FactorFor(unit);

        /// <summary>Converts a length expressed in millimetres into <paramref name="unit"/>.</summary>
        /// <param name="millimetres">The value to convert, in millimetres.</param>
        /// <param name="unit">The target unit.</param>
        /// <returns>The equivalent length in <paramref name="unit"/>.</returns>
        public static decimal FromMillimetres(decimal millimetres, LengthUnit unit) => millimetres / FactorFor(unit);

        /// <summary>Converts a mass expressed in <paramref name="unit"/> into kilograms.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
        /// <returns>The equivalent mass in kilograms.</returns>
        public static decimal ToKilograms(decimal value, MassUnit unit) => value * FactorFor(unit);

        /// <summary>Converts a mass expressed in kilograms into <paramref name="unit"/>.</summary>
        /// <param name="kilograms">The value to convert, in kilograms.</param>
        /// <param name="unit">The target unit.</param>
        /// <returns>The equivalent mass in <paramref name="unit"/>.</returns>
        public static decimal FromKilograms(decimal kilograms, MassUnit unit) => kilograms / FactorFor(unit);

        /// <summary>Converts a length between two arbitrary supported units.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="from">The unit <paramref name="value"/> is expressed in.</param>
        /// <param name="to">The target unit.</param>
        /// <returns>The converted length.</returns>
        public static decimal ConvertLength(decimal value, LengthUnit from, LengthUnit to) =>
            from == to ? value : FromMillimetres(ToMillimetres(value, from), to);

        /// <summary>Converts a mass between two arbitrary supported units.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="from">The unit <paramref name="value"/> is expressed in.</param>
        /// <param name="to">The target unit.</param>
        /// <returns>The converted mass.</returns>
        public static decimal ConvertMass(decimal value, MassUnit from, MassUnit to) =>
            from == to ? value : FromKilograms(ToKilograms(value, from), to);

        /// <summary>Gets the conventional abbreviation for a length unit, for example <c>mm</c>.</summary>
        /// <param name="unit">The unit to describe.</param>
        /// <returns>The abbreviation.</returns>
        public static string Abbreviation(LengthUnit unit) =>
            LengthAbbreviations.TryGetValue(unit, out string? text)
                ? text
                : throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit.");

        /// <summary>Gets the conventional abbreviation for a mass unit, for example <c>kg</c>.</summary>
        /// <param name="unit">The unit to describe.</param>
        /// <returns>The abbreviation.</returns>
        public static string Abbreviation(MassUnit unit) =>
            MassAbbreviations.TryGetValue(unit, out string? text)
                ? text
                : throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported mass unit.");

        /// <summary>
        /// Resolves a unit from the textual token used in the JSON database, for example
        /// <c>mm</c>, <c>millimetre</c> or <c>Millimeter</c>.
        /// </summary>
        /// <param name="token">The token to parse.</param>
        /// <param name="unit">Receives the parsed unit when parsing succeeds.</param>
        /// <returns><see langword="true"/> when <paramref name="token"/> was recognised.</returns>
        public static bool TryParseLengthUnit(string? token, out LengthUnit unit)
        {
            unit = LengthUnit.Millimetre;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "mm":
                case "millimetre":
                case "millimetres":
                case "millimeter":
                case "millimeters":
                    unit = LengthUnit.Millimetre;
                    return true;
                case "cm":
                case "centimetre":
                case "centimetres":
                case "centimeter":
                case "centimeters":
                    unit = LengthUnit.Centimetre;
                    return true;
                case "m":
                case "metre":
                case "metres":
                case "meter":
                case "meters":
                    unit = LengthUnit.Metre;
                    return true;
                case "in":
                case "inch":
                case "inches":
                    unit = LengthUnit.Inch;
                    return true;
                case "ft":
                case "foot":
                case "feet":
                    unit = LengthUnit.Foot;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Resolves a mass unit from a textual token used in the JSON database.</summary>
        /// <param name="token">The token to parse.</param>
        /// <param name="unit">Receives the parsed unit when parsing succeeds.</param>
        /// <returns><see langword="true"/> when <paramref name="token"/> was recognised.</returns>
        public static bool TryParseMassUnit(string? token, out MassUnit unit)
        {
            unit = MassUnit.Kilogram;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "g":
                case "gram":
                case "grams":
                    unit = MassUnit.Gram;
                    return true;
                case "kg":
                case "kilogram":
                case "kilograms":
                    unit = MassUnit.Kilogram;
                    return true;
                case "t":
                case "te":
                case "tonne":
                case "tonnes":
                case "metric ton":
                    unit = MassUnit.Tonne;
                    return true;
                case "lb":
                case "lbs":
                case "pound":
                case "pounds":
                    unit = MassUnit.Pound;
                    return true;
                default:
                    return false;
            }
        }

        private static decimal FactorFor(LengthUnit unit) =>
            MillimetresPerLengthUnit.TryGetValue(unit, out decimal factor)
                ? factor
                : throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit.");

        private static decimal FactorFor(MassUnit unit) =>
            KilogramsPerMassUnit.TryGetValue(unit, out decimal factor)
                ? factor
                : throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported mass unit.");
    }
}
