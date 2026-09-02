using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Units;

namespace StructureGen.Bom
{
    /// <summary>One line of the finished schedule.</summary>
    public sealed class BomLine
    {
        /// <summary>Initialises a new instance of the <see cref="BomLine"/> class.</summary>
        /// <param name="itemNumber">The line number, counting from one.</param>
        /// <param name="entries">The occurrences collapsed onto this line.</param>
        public BomLine(int itemNumber, IReadOnlyList<BomEntry> entries)
        {
            ItemNumber = itemNumber;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));

            if (entries.Count == 0)
            {
                throw new ArgumentException("A bill of materials line needs at least one occurrence.", nameof(entries));
            }

            Definition = entries[0].Definition;
            Quantity = entries.Sum(e => e.Definition.Quantity);

            WeightSource = entries.Select(e => e.Mass.Source).Distinct().Count() == 1
                ? entries[0].Mass.Source
                : Core.Model.WeightSource.Unavailable;

            MassIsKnown = entries.All(e => e.Mass.IsKnown);
            TotalMassKg = MassIsKnown ? entries.Sum(e => e.Mass.TotalMassKg ?? 0m) : null;
            UnitWeightKgPerM = entries[0].Mass.UnitWeightKgPerM;
            PieceMassKg = entries[0].Mass.PieceMassKg;
            MassExplanation = BuildExplanation(entries);
        }

        /// <summary>Gets the line number, counting from one.</summary>
        public int ItemNumber { get; }

        /// <summary>Gets the occurrences collapsed onto this line.</summary>
        public IReadOnlyList<BomEntry> Entries { get; }

        /// <summary>Gets the definition that describes this line.</summary>
        public ElementDefinition Definition { get; }

        /// <summary>Gets the total number of pieces on this line.</summary>
        public int Quantity { get; }

        /// <summary>Gets the identifiers of every element collapsed onto this line.</summary>
        public IReadOnlyList<Guid> ElementIds => Entries.Select(e => e.Definition.ElementId).Distinct().ToList();

        /// <summary>Gets the AutoCAD handles of every object collapsed onto this line.</summary>
        public IReadOnlyList<string> Handles =>
            Entries.Where(e => e.Handle is not null).Select(e => e.Handle!).ToList();

        /// <summary>Gets the element category identifier.</summary>
        public string CategoryId => Definition.CategoryId;

        /// <summary>Gets the display name of the element category.</summary>
        public string CategoryName => CategoryCatalog.FindOrGeneric(CategoryId).DisplayName;

        /// <summary>Gets the identifier of the standard the profile comes from.</summary>
        public string StandardId => Definition.StandardId;

        /// <summary>Gets the profile designation.</summary>
        public string Designation => Definition.Designation;

        /// <summary>Gets the member length in millimetres, when the category has one.</summary>
        public decimal? LengthMm => Definition.LengthMm;

        /// <summary>Gets the total length of every piece on this line, in millimetres.</summary>
        public decimal? TotalLengthMm => LengthMm.HasValue ? LengthMm.Value * Quantity : null;

        /// <summary>Gets the tabulated mass per metre, when one applies.</summary>
        public decimal? UnitWeightKgPerM { get; }

        /// <summary>Gets the mass of a single piece, when one applies.</summary>
        public decimal? PieceMassKg { get; }

        /// <summary>Gets the total mass of this line in kilograms, or <see langword="null"/> when unknown.</summary>
        public decimal? TotalMassKg { get; }

        /// <summary>Gets the total mass of this line in metric tonnes, or <see langword="null"/> when unknown.</summary>
        public decimal? TotalMassTonnes =>
            TotalMassKg.HasValue ? UnitConverter.FromKilograms(TotalMassKg.Value, MassUnit.Tonne) : null;

        /// <summary>Gets a value indicating whether a usable mass is available for every occurrence.</summary>
        public bool MassIsKnown { get; }

        /// <summary>Gets how the mass was arrived at.</summary>
        public WeightSource WeightSource { get; }

        /// <summary>Gets a description of exactly how the total mass was computed.</summary>
        public string MassExplanation { get; }

        /// <summary>Gets the material name.</summary>
        public string? Material => Definition.Material;

        /// <summary>Gets the material grade.</summary>
        public string? Grade => Definition.Grade;

        /// <summary>Gets the layer, when every occurrence shares one.</summary>
        public string? Layer =>
            Entries.Select(e => e.Layer).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? Entries[0].Layer
                : null;

        /// <summary>Gets the free text notes.</summary>
        public string? Notes => Definition.Notes;

        /// <summary>Gets the drawing reference.</summary>
        public string? DrawingReference => Definition.DrawingReference;

        /// <summary>Gets a value indicating whether this line departs from the standard profile.</summary>
        public bool IsCustom => Definition.IsCustom;

        /// <summary>Gets a value indicating whether the source data was marked verified.</summary>
        public bool SourceDataVerified => Definition.SourceDataVerified;

        /// <summary>
        /// Gets a short, human-readable summary of the standardised cross-section, for example
        /// <c>300 x 150 x 7.1 x 10.7</c>.
        /// </summary>
        public string DimensionSummary => DimensionSummaryFormatter.Describe(Definition);

        /// <summary>Expresses the member length in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted length, or <see langword="null"/> when the category has none.</returns>
        public decimal? LengthIn(LengthUnit unit) =>
            LengthMm.HasValue ? UnitConverter.FromMillimetres(LengthMm.Value, unit) : null;

        /// <summary>Expresses the total length in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted total length, or <see langword="null"/> when the category has none.</returns>
        public decimal? TotalLengthIn(LengthUnit unit) =>
            TotalLengthMm.HasValue ? UnitConverter.FromMillimetres(TotalLengthMm.Value, unit) : null;

        /// <summary>Expresses the total mass in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted mass, or <see langword="null"/> when the mass is unknown.</returns>
        public decimal? TotalMassIn(MassUnit unit) =>
            TotalMassKg.HasValue ? UnitConverter.FromKilograms(TotalMassKg.Value, unit) : null;

        private static string BuildExplanation(IReadOnlyList<BomEntry> entries)
        {
            if (entries.Count == 1)
            {
                return entries[0].Mass.Explanation;
            }

            List<string> distinct = entries.Select(e => e.Mass.Explanation).Distinct(StringComparer.Ordinal).ToList();
            return distinct.Count == 1
                ? $"{distinct[0]}, summed over {entries.Count} occurrence(s)"
                : string.Join("; ", distinct);
        }
    }
}
