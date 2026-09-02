using System;

namespace StructureGen.Data
{
    /// <summary>Constants describing the on-disk database schema.</summary>
    public static class DatabaseSchema
    {
        /// <summary>The schema version this build reads and writes.</summary>
        public const string CurrentVersion = "1.0";

        /// <summary>Gets the major component of <see cref="CurrentVersion"/>.</summary>
        public static int CurrentMajorVersion => MajorOf(CurrentVersion) ?? 1;

        /// <summary>
        /// Extracts the major component of a schema version string.
        /// </summary>
        /// <param name="version">The version string, for example <c>1.2</c>.</param>
        /// <returns>The major component, or <see langword="null"/> when the string is not a version.</returns>
        public static int? MajorOf(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

            string head = version!.Split('.')[0].Trim();
            return int.TryParse(head, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int major)
                ? major
                : null;
        }

        /// <summary>
        /// Determines whether a file claiming <paramref name="version"/> can be read by this build.
        /// </summary>
        /// <param name="version">The schema version declared by the file.</param>
        /// <returns><see langword="true"/> when the major versions match.</returns>
        /// <remarks>
        /// A newer minor version is accepted: minor revisions only ever add optional fields, which
        /// an older reader ignores. A different major version is rejected outright rather than
        /// silently misread.
        /// </remarks>
        public static bool IsReadable(string? version)
        {
            int? major = MajorOf(version);
            return major.HasValue && major.Value == CurrentMajorVersion;
        }
    }

    /// <summary>
    /// Parses the unit tokens declared in a database file header and converts values into the
    /// canonical internal units.
    /// </summary>
    /// <remarks>
    /// This is what lets an imperial dataset be dropped in unchanged: the file declares its own
    /// units once, and every number it contains is converted on load. Nothing downstream ever has
    /// to ask which unit a value is in.
    /// </remarks>
    public sealed class DatabaseUnits
    {
        private DatabaseUnits(
            Core.Units.LengthUnit lengthUnit,
            decimal unitWeightToKgPerMetre,
            decimal densityToKgPerCubicMetre,
            Core.Units.MassUnit pieceMassUnit,
            string unitWeightToken,
            string densityToken)
        {
            LengthUnit = lengthUnit;
            UnitWeightToKgPerMetre = unitWeightToKgPerMetre;
            DensityToKgPerCubicMetre = densityToKgPerCubicMetre;
            PieceMassUnit = pieceMassUnit;
            UnitWeightToken = unitWeightToken;
            DensityToken = densityToken;
        }

        /// <summary>Gets the default units: millimetres, kilograms per metre, kilograms per cubic metre.</summary>
        public static DatabaseUnits Default { get; } = new(
            Core.Units.LengthUnit.Millimetre, 1m, 1m, Core.Units.MassUnit.Kilogram, "kg/m", "kg/m3");

        /// <summary>Gets the unit lengths in the file are expressed in.</summary>
        public Core.Units.LengthUnit LengthUnit { get; }

        /// <summary>Gets the factor converting the file's mass-per-length values to kilograms per metre.</summary>
        public decimal UnitWeightToKgPerMetre { get; }

        /// <summary>Gets the factor converting the file's density values to kilograms per cubic metre.</summary>
        public decimal DensityToKgPerCubicMetre { get; }

        /// <summary>Gets the unit per-piece masses in the file are expressed in.</summary>
        public Core.Units.MassUnit PieceMassUnit { get; }

        /// <summary>Gets the mass-per-length token as it appeared in the file.</summary>
        public string UnitWeightToken { get; }

        /// <summary>Gets the density token as it appeared in the file.</summary>
        public string DensityToken { get; }

        /// <summary>Converts a length from the file's unit into millimetres.</summary>
        /// <param name="value">The value as written in the file.</param>
        /// <returns>The value in millimetres.</returns>
        public decimal LengthToMillimetres(decimal value) =>
            Core.Units.UnitConverter.ToMillimetres(value, LengthUnit);

        /// <summary>Converts a mass-per-length from the file's unit into kilograms per metre.</summary>
        /// <param name="value">The value as written in the file.</param>
        /// <returns>The value in kilograms per metre.</returns>
        public decimal UnitWeightToKgPerM(decimal value) => value * UnitWeightToKgPerMetre;

        /// <summary>Converts a density from the file's unit into kilograms per cubic metre.</summary>
        /// <param name="value">The value as written in the file.</param>
        /// <returns>The value in kilograms per cubic metre.</returns>
        public decimal DensityToKgPerM3(decimal value) => value * DensityToKgPerCubicMetre;

        /// <summary>Converts a per-piece mass from the file's unit into kilograms.</summary>
        /// <param name="value">The value as written in the file.</param>
        /// <returns>The value in kilograms.</returns>
        public decimal PieceMassToKg(decimal value) =>
            Core.Units.UnitConverter.ToKilograms(value, PieceMassUnit);

        /// <summary>
        /// Builds a unit set from the tokens declared in a database file.
        /// </summary>
        /// <param name="lengthToken">The length token, for example <c>mm</c> or <c>in</c>.</param>
        /// <param name="unitWeightToken">The mass-per-length token, for example <c>kg/m</c> or <c>lb/ft</c>.</param>
        /// <param name="densityToken">The density token, for example <c>kg/m3</c> or <c>lb/in3</c>.</param>
        /// <param name="pieceMassToken">The per-piece mass token, for example <c>kg</c> or <c>g</c>.</param>
        /// <param name="unrecognisedToken">Receives the first token that could not be understood.</param>
        /// <returns>The unit set, falling back to the default for any token that is absent.</returns>
        public static DatabaseUnits Parse(
            string? lengthToken,
            string? unitWeightToken,
            string? densityToken,
            string? pieceMassToken,
            out string? unrecognisedToken)
        {
            unrecognisedToken = null;

            Core.Units.LengthUnit lengthUnit = Default.LengthUnit;
            if (!string.IsNullOrWhiteSpace(lengthToken) &&
                !Core.Units.UnitConverter.TryParseLengthUnit(lengthToken, out lengthUnit))
            {
                unrecognisedToken ??= lengthToken;
                lengthUnit = Default.LengthUnit;
            }

            decimal unitWeightFactor = Default.UnitWeightToKgPerMetre;
            string resolvedUnitWeightToken = Default.UnitWeightToken;
            if (!string.IsNullOrWhiteSpace(unitWeightToken))
            {
                decimal? factor = MassPerLengthFactor(unitWeightToken!);
                if (factor.HasValue)
                {
                    unitWeightFactor = factor.Value;
                    resolvedUnitWeightToken = unitWeightToken!;
                }
                else
                {
                    unrecognisedToken ??= unitWeightToken;
                }
            }

            decimal densityFactor = Default.DensityToKgPerCubicMetre;
            string resolvedDensityToken = Default.DensityToken;
            if (!string.IsNullOrWhiteSpace(densityToken))
            {
                decimal? factor = DensityFactor(densityToken!);
                if (factor.HasValue)
                {
                    densityFactor = factor.Value;
                    resolvedDensityToken = densityToken!;
                }
                else
                {
                    unrecognisedToken ??= densityToken;
                }
            }

            Core.Units.MassUnit pieceMassUnit = Default.PieceMassUnit;
            if (!string.IsNullOrWhiteSpace(pieceMassToken) &&
                !Core.Units.UnitConverter.TryParseMassUnit(pieceMassToken, out pieceMassUnit))
            {
                unrecognisedToken ??= pieceMassToken;
                pieceMassUnit = Default.PieceMassUnit;
            }

            return new DatabaseUnits(
                lengthUnit, unitWeightFactor, densityFactor, pieceMassUnit,
                resolvedUnitWeightToken, resolvedDensityToken);
        }

        private static decimal? MassPerLengthFactor(string token)
        {
            string[] parts = token.Replace(" ", string.Empty).Split('/');
            if (parts.Length != 2)
            {
                return null;
            }

            if (!Core.Units.UnitConverter.TryParseMassUnit(parts[0], out Core.Units.MassUnit mass) ||
                !Core.Units.UnitConverter.TryParseLengthUnit(parts[1], out Core.Units.LengthUnit length))
            {
                return null;
            }

            decimal kilogramsPerUnitMass = Core.Units.UnitConverter.ToKilograms(1m, mass);
            decimal metresPerUnitLength = Core.Units.UnitConverter.FromMillimetres(
                Core.Units.UnitConverter.ToMillimetres(1m, length), Core.Units.LengthUnit.Metre);
            return kilogramsPerUnitMass / metresPerUnitLength;
        }

        private static decimal? DensityFactor(string token)
        {
            string normalised = token.Replace(" ", string.Empty)
                                     .Replace("^", string.Empty)
                                     .Replace("³", "3")
                                     .ToLowerInvariant();

            string[] parts = normalised.Split('/');
            if (parts.Length != 2 || !parts[1].EndsWith("3", StringComparison.Ordinal))
            {
                return null;
            }

            string lengthToken = parts[1].Substring(0, parts[1].Length - 1);
            if (!Core.Units.UnitConverter.TryParseMassUnit(parts[0], out Core.Units.MassUnit mass) ||
                !Core.Units.UnitConverter.TryParseLengthUnit(lengthToken, out Core.Units.LengthUnit length))
            {
                return null;
            }

            decimal kilogramsPerUnitMass = Core.Units.UnitConverter.ToKilograms(1m, mass);
            decimal metresPerUnitLength = Core.Units.UnitConverter.FromMillimetres(
                Core.Units.UnitConverter.ToMillimetres(1m, length), Core.Units.LengthUnit.Metre);
            decimal cubicMetresPerUnitVolume = metresPerUnitLength * metresPerUnitLength * metresPerUnitLength;
            return kilogramsPerUnitMass / cubicMetresPerUnitVolume;
        }
    }
}
