using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Data;
using StructureGen.Geometry;

namespace StructureGen.Services
{
    /// <summary>Builds validated <see cref="ElementDefinition"/> instances from a database record.</summary>
    /// <remarks>
    /// Creating an element in one place means the rules about what is copied from the record, what
    /// the user supplies and what is derived hold identically whether the element came from the
    /// palette, from a script, or from the metadata read back off an existing drawing object.
    /// </remarks>
    public sealed class ElementFactory
    {
        private readonly DatabaseService _database;
        private readonly ElementGeneratorRegistry _generators;

        /// <summary>Initialises a new instance of the <see cref="ElementFactory"/> class.</summary>
        /// <param name="database">Supplies the records and their materials.</param>
        /// <param name="generators">Supplies the parameter schema and validation for each category.</param>
        public ElementFactory(DatabaseService database, ElementGeneratorRegistry generators)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _generators = generators ?? throw new ArgumentNullException(nameof(generators));
        }

        /// <summary>Creates an element from a record and the values the user supplied.</summary>
        /// <param name="record">The standard profile to base the element on.</param>
        /// <param name="lengthMm">The member length, where the category has one.</param>
        /// <param name="quantity">How many pieces.</param>
        /// <param name="detailLevel">How much geometric detail to generate.</param>
        /// <param name="representation">Which representations to generate.</param>
        /// <param name="placements">Where the pieces go. May be empty.</param>
        /// <param name="dimensionOverrides">
        /// Dimensions the user has changed. Only honoured for adjustable parameters, or for any
        /// parameter when <paramref name="isCustom"/> is set.
        /// </param>
        /// <param name="layer">The target layer, or <see langword="null"/> for the automatic layer.</param>
        /// <param name="notes">Free text carried to the bill of materials.</param>
        /// <param name="drawingReference">Free text carried to the bill of materials.</param>
        /// <param name="isCustom">Whether the user has deliberately departed from the standard profile.</param>
        /// <param name="elementId">
        /// The identity to preserve. Supply the existing identity when editing so that the element
        /// keeps the same identity across the change; omit it to mint a new one.
        /// </param>
        /// <returns>The element, or a failure describing why the values are not usable.</returns>
        public Result<ElementDefinition> Create(
            SectionRecord record,
            decimal? lengthMm,
            int quantity,
            DetailLevel detailLevel = DetailLevel.Standard,
            RepresentationMode representation = RepresentationMode.Solid3D,
            IReadOnlyList<Placement>? placements = null,
            IReadOnlyDictionary<string, decimal>? dimensionOverrides = null,
            string? layer = null,
            string? notes = null,
            string? drawingReference = null,
            bool isCustom = false,
            Guid? elementId = null)
        {
            if (record is null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            IElementGenerator? generator = _generators.Find(record.CategoryId);
            if (generator is null)
            {
                return Result<ElementDefinition>.Failure(
                    "Element.NoGenerator",
                    $"'{record.Designation}' belongs to the category '{record.CategoryId}', which this build of " +
                    "StructureGen cannot generate geometry for. It can still be scheduled, but not drawn.");
            }

            ParameterSchema schema = generator.GetParameterSchema(record);
            Dictionary<string, decimal> dimensions = record.CopyDimensions();
            ValidationReport report = new();

            if (dimensionOverrides is not null)
            {
                ApplyOverrides(record, schema, dimensions, dimensionOverrides, isCustom, report);
            }

            if (report.HasErrors)
            {
                return Result<ElementDefinition>.Failure(
                    "Element.InvalidOverride",
                    $"'{record.Designation}' could not be configured:{Environment.NewLine}{report.ToDisplayString()}",
                    report);
            }

            MaterialRecord? material = _database.Current.ResolveMaterial(record);

            ElementDefinition definition = new(
                elementId ?? Guid.NewGuid(),
                record.CategoryId,
                record.StandardId,
                record.Id,
                record.Designation,
                dimensions,
                quantity,
                lengthMm,
                detailLevel,
                representation,
                placements,
                material?.Name ?? record.Material,
                record.Grade ?? material?.Grade,
                record.UnitWeightKgPerM,
                record.PieceMassKg,
                material?.DensityKgPerCubicMetre,
                layer,
                notes,
                drawingReference,
                isCustom,
                record.Verified);

            ValidationReport validation = generator.Validate(definition, record);
            if (validation.HasErrors)
            {
                return Result<ElementDefinition>.Failure(
                    "Element.Invalid",
                    $"'{record.Designation}' is not valid:{Environment.NewLine}{validation.ToDisplayString()}",
                    validation);
            }

            return Result<ElementDefinition>.Success(definition);
        }

        /// <summary>Validates an existing definition against the record it claims to come from.</summary>
        /// <param name="definition">The definition to check.</param>
        /// <returns>A report. An empty report means the definition is sound.</returns>
        public ValidationReport Validate(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IElementGenerator? generator = _generators.Find(definition.CategoryId);
            if (generator is null)
            {
                ValidationReport report = new();
                report.AddError(
                    "Element.NoGenerator",
                    $"No geometry generator is registered for the category '{definition.CategoryId}'.",
                    definition.Designation);
                return report;
            }

            return generator.Validate(definition, _database.FindRecord(definition.SectionRecordId));
        }

        private static void ApplyOverrides(
            SectionRecord record,
            ParameterSchema schema,
            IDictionary<string, decimal> dimensions,
            IReadOnlyDictionary<string, decimal> overrides,
            bool isCustom,
            ValidationReport report)
        {
            foreach (KeyValuePair<string, decimal> pair in overrides)
            {
                if (InstanceParameterKeys.IsInstanceKey(pair.Key))
                {
                    continue;
                }

                ParameterDescriptor? descriptor = schema.Find(pair.Key);

                if (descriptor is null)
                {
                    report.AddWarning(
                        "Element.UnknownParameter",
                        $"'{pair.Key}' is not a parameter of a {record.CategoryId}, so it was ignored.",
                        record.Designation);
                    continue;
                }

                if (descriptor.Mutability == ParameterMutability.StandardLocked && !isCustom)
                {
                    decimal standard = record.Dimension(pair.Key) ?? 0m;
                    report.AddError(
                        "Element.LockedParameter",
                        $"'{descriptor.DisplayName}' is fixed by {record.StandardId} at {standard} mm and cannot be " +
                        "changed. Switch the item to custom mode if you intend to depart from the standard profile.",
                        record.Designation);
                    continue;
                }

                if (pair.Value <= 0m)
                {
                    report.AddError(
                        "Element.NonPositiveParameter",
                        $"'{descriptor.DisplayName}' must be greater than zero.",
                        record.Designation);
                    continue;
                }

                if (descriptor.Minimum is { } minimum && pair.Value < minimum)
                {
                    report.AddError(
                        "Element.BelowMinimum",
                        $"'{descriptor.DisplayName}' must be at least {minimum} mm; {pair.Value} mm was given.",
                        record.Designation);
                    continue;
                }

                if (descriptor.Maximum is { } maximum && pair.Value > maximum)
                {
                    report.AddError(
                        "Element.AboveMaximum",
                        $"'{descriptor.DisplayName}' must be at most {maximum} mm; {pair.Value} mm was given.",
                        record.Designation);
                    continue;
                }

                dimensions[pair.Key] = pair.Value;
            }
        }
    }
}
