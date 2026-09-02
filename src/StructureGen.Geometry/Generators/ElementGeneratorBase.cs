using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Shared behaviour for every element generator: parameter validation, the guard that stops a
    /// standard profile being edited into a non-standard one, and the mass calculation.
    /// </summary>
    public abstract class ElementGeneratorBase : IElementGenerator
    {
        /// <summary>The greatest member length accepted, in millimetres. One kilometre.</summary>
        protected const decimal MaximumLengthMm = 1_000_000m;

        /// <summary>The member length above which a warning is raised, in millimetres. One hundred metres.</summary>
        protected const decimal ImplausibleLengthMm = 100_000m;

        /// <summary>The greatest quantity accepted in a single definition.</summary>
        protected const int MaximumQuantity = 100_000;

        /// <summary>Tolerance, in millimetres, within which a dimension counts as unchanged.</summary>
        protected const decimal DimensionTolerance = 0.0005m;

        /// <inheritdoc />
        public abstract string CategoryId { get; }

        /// <inheritdoc />
        public abstract IReadOnlyList<string> RequiredDimensions { get; }

        /// <summary>Gets how this category behaves parametrically.</summary>
        protected CategoryDescriptor Descriptor => CategoryCatalog.FindOrGeneric(CategoryId);

        /// <inheritdoc />
        public abstract ParameterSchema GetParameterSchema(SectionRecord record);

        /// <inheritdoc />
        public Result<ElementGeometry> BuildGeometry(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ValidationReport report = Validate(definition);
            if (report.HasErrors)
            {
                return Result<ElementGeometry>.Failure(
                    "Geometry.InvalidParameters",
                    $"'{definition.Designation}' cannot be generated because its parameters are not valid." +
                    Environment.NewLine + report.ToDisplayString(),
                    report);
            }

            try
            {
                return Result<ElementGeometry>.Success(BuildGeometryCore(definition));
            }
            catch (ArgumentException exception)
            {
                return GeometryFailure(definition, exception);
            }
            catch (InvalidOperationException exception)
            {
                return GeometryFailure(definition, exception);
            }
            catch (ArithmeticException exception)
            {
                return GeometryFailure(definition, exception);
            }
        }

        /// <summary>Builds the geometry for a definition that has already been validated.</summary>
        /// <param name="definition">The definition to realise.</param>
        /// <returns>The geometry.</returns>
        protected abstract ElementGeometry BuildGeometryCore(ElementDefinition definition);

        /// <inheritdoc />
        public virtual ValidationReport Validate(ElementDefinition definition, SectionRecord? record = null)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ValidationReport report = new();

            ValidateQuantity(definition, report);
            ValidateLength(definition, report);
            ValidateRequiredDimensions(definition, report);
            ValidateStandardConformance(definition, record, report);
            ValidateCore(definition, report);

            return report;
        }

        /// <summary>Adds any checks specific to one category.</summary>
        /// <param name="definition">The definition being checked.</param>
        /// <param name="report">The report to add findings to.</param>
        protected virtual void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            // The base checks are sufficient for most categories.
        }

        /// <inheritdoc />
        public virtual MassProperties ComputeMass(ElementDefinition definition, ElementGeometry? geometry)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int quantity = Math.Max(0, definition.Quantity);
            double area = geometry?.PrimaryProfile.AreaMm2 ?? 0d;
            double volume = geometry?.VolumeMm3 ?? 0d;

            if (Descriptor.Kind == CategoryKind.Fastener)
            {
                return ComputeFastenerMass(definition, quantity, area, volume);
            }

            if (definition.UnitWeightKgPerM is { } unitWeight && unitWeight > 0m && definition.LengthMm is { } lengthMm)
            {
                decimal lengthMetres = Round(lengthMm / 1000m, 6);
                decimal total = Round(unitWeight * lengthMetres * quantity, 3);
                return new MassProperties(
                    WeightSource.UnitWeightTable,
                    unitWeight,
                    Round(unitWeight * lengthMetres, 3),
                    total,
                    area,
                    volume,
                    $"{Format(unitWeight)} kg/m x {Format(lengthMetres)} m x {quantity} = {Format(total)} kg");
            }

            return ComputeFromDensity(definition, quantity, area, volume);
        }

        private MassProperties ComputeFastenerMass(ElementDefinition definition, int quantity, double area, double volume)
        {
            if (definition.PieceMassKg is { } pieceMass && pieceMass > 0m)
            {
                decimal total = Round(pieceMass * quantity, 3);
                return new MassProperties(
                    WeightSource.PieceMassTable,
                    null,
                    pieceMass,
                    total,
                    area,
                    volume,
                    $"{Format(pieceMass)} kg/piece x {quantity} = {Format(total)} kg");
            }

            return ComputeFromDensity(definition, quantity, area, volume);
        }

        private static MassProperties ComputeFromDensity(
            ElementDefinition definition, int quantity, double area, double volume)
        {
            if (definition.DensityKgPerCubicMetre is not { } density || density <= 0m)
            {
                return MassProperties.Unavailable(
                    "No mass per metre, no mass per piece and no material density are available for this profile, " +
                    "so its weight is reported as unknown rather than estimated.",
                    area,
                    volume);
            }

            if (volume <= 0d)
            {
                return MassProperties.Unavailable(
                    "The generated volume is zero, so no weight can be derived from the material density.",
                    area,
                    volume);
            }

            // A cubic metre is 1e9 cubic millimetres.
            decimal cubicMetres = Round((decimal)volume / 1_000_000_000m, 9);
            decimal pieceMass = Round(cubicMetres * density, 6);
            decimal total = Round(pieceMass * quantity, 3);

            string perMetre = string.Empty;
            decimal? unitWeight = null;
            if (definition.LengthMm is { } lengthMm && lengthMm > 0m)
            {
                unitWeight = Round(pieceMass / (lengthMm / 1000m), 4);
                perMetre = $" (equivalent to {Format(unitWeight.Value)} kg/m)";
            }

            return new MassProperties(
                WeightSource.ComputedFromDensity,
                unitWeight,
                pieceMass,
                total,
                area,
                volume,
                $"{Format(cubicMetres)} m3/piece x {Format(density)} kg/m3 x {quantity} = {Format(total)} kg{perMetre}");
        }

        private void ValidateQuantity(ElementDefinition definition, ValidationReport report)
        {
            if (definition.Quantity < 1)
            {
                report.AddError(
                    "Element.QuantityTooSmall",
                    $"The quantity must be at least 1; {definition.Quantity} was given.",
                    definition.Designation);
            }
            else if (definition.Quantity > MaximumQuantity)
            {
                report.AddError(
                    "Element.QuantityTooLarge",
                    $"The quantity must not exceed {MaximumQuantity:N0}; {definition.Quantity:N0} was given. " +
                    "Split the item into several lines if you genuinely need more.",
                    definition.Designation);
            }
        }

        private void ValidateLength(ElementDefinition definition, ValidationReport report)
        {
            if (!Descriptor.LengthIsUserParameter)
            {
                return;
            }

            if (definition.LengthMm is not { } length)
            {
                report.AddError(
                    "Element.LengthMissing",
                    $"'{definition.Designation}' needs a length.",
                    definition.Designation);
                return;
            }

            if (length <= 0m)
            {
                report.AddError(
                    "Element.LengthNotPositive",
                    $"The length must be greater than zero; {Format(length)} mm was given.",
                    definition.Designation);
            }
            else if (length > MaximumLengthMm)
            {
                report.AddError(
                    "Element.LengthTooLarge",
                    $"The length must not exceed {Format(MaximumLengthMm)} mm; {Format(length)} mm was given. " +
                    "Check that the value was entered in millimetres.",
                    definition.Designation);
            }
            else if (length > ImplausibleLengthMm)
            {
                report.AddWarning(
                    "Element.LengthImplausible",
                    $"A length of {Format(length / 1000m)} m is unusually long for a single member. " +
                    "Check that the value was entered in millimetres.",
                    definition.Designation);
            }
        }

        private void ValidateRequiredDimensions(ElementDefinition definition, ValidationReport report)
        {
            List<string> missing = RequiredDimensions
                .Where(key => definition.Dimension(key) is not { } value || value <= 0m)
                .ToList();

            if (missing.Count > 0)
            {
                report.AddError(
                    "Element.MissingDimension",
                    $"'{definition.Designation}' is missing the dimension(s) {string.Join(", ", missing)}, " +
                    "which are required to generate its geometry. Check the database record.",
                    definition.Designation);
            }
        }

        /// <summary>
        /// Rejects any change to a standardised dimension unless the element has been switched into
        /// custom mode.
        /// </summary>
        /// <param name="definition">The definition being checked.</param>
        /// <param name="record">The record it claims to come from, or <see langword="null"/> when unavailable.</param>
        /// <param name="report">The report to add findings to.</param>
        /// <remarks>
        /// This is the second half of the guard whose first half is the read-only control in the
        /// palette. Enforcing it here as well means a definition built in code, restored from
        /// metadata on an object, or edited by a future feature cannot bypass it.
        /// </remarks>
        protected void ValidateStandardConformance(
            ElementDefinition definition, SectionRecord? record, ValidationReport report)
        {
            if (record is null || definition.IsCustom)
            {
                return;
            }

            ParameterSchema schema = GetParameterSchema(record);

            foreach (ParameterDescriptor parameter in schema.StandardDimensions)
            {
                decimal? standard = record.Dimension(parameter.Key);
                decimal? actual = definition.Dimension(parameter.Key);

                if (standard is null || actual is null)
                {
                    continue;
                }

                if (Math.Abs(standard.Value - actual.Value) > DimensionTolerance)
                {
                    report.AddError(
                        "Element.StandardDimensionAltered",
                        $"'{parameter.DisplayName}' is fixed by {record.StandardId} for profile " +
                        $"'{record.Designation}' at {Format(standard.Value)} mm, but {Format(actual.Value)} mm was given. " +
                        "Switch the item to custom mode if you intend to depart from the standard profile.",
                        definition.Designation);
                }
            }
        }

        /// <summary>Reads a dimension as a <see cref="double"/>, falling back when it is absent.</summary>
        /// <param name="definition">The definition to read from.</param>
        /// <param name="key">The dimension key.</param>
        /// <param name="fallback">The value to use when the dimension is absent.</param>
        /// <returns>The dimension in millimetres.</returns>
        protected static double Dimension(ElementDefinition definition, string key, double fallback = 0d) =>
            definition.Dimension(key) is { } value ? (double)value : fallback;

        /// <summary>Reads the geometry radius to apply for a given detail level.</summary>
        /// <param name="definition">The definition to read from.</param>
        /// <param name="key">The dimension key holding the radius.</param>
        /// <param name="minimumLevel">The level at or above which the radius is honoured.</param>
        /// <returns>The radius in millimetres, or zero when the detail level suppresses it.</returns>
        protected static double RadiusForDetail(ElementDefinition definition, string key, DetailLevel minimumLevel) =>
            definition.DetailLevel >= minimumLevel ? Dimension(definition, key) : 0d;

        /// <summary>Rounds a decimal half away from zero.</summary>
        /// <param name="value">The value to round.</param>
        /// <param name="digits">The number of decimal places to keep.</param>
        /// <returns>The rounded value.</returns>
        protected static decimal Round(decimal value, int digits) =>
            Math.Round(value, digits, MidpointRounding.AwayFromZero);

        /// <summary>Formats a number for a user-facing message, without a thousands separator.</summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The formatted text.</returns>
        protected static string Format(decimal value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);

        private Result<ElementGeometry> GeometryFailure(ElementDefinition definition, Exception exception) =>
            Result<ElementGeometry>.Failure(
                "Geometry.BuildFailed",
                $"The geometry for '{definition.Designation}' could not be built: {exception.Message}");
    }
}
