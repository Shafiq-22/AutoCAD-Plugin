using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Units;

namespace StructureGen.Bom
{
    /// <summary>The totals across a set of schedule lines.</summary>
    /// <remarks>
    /// A line whose mass could not be established contributes nothing to
    /// <see cref="TotalMassKg"/> and is counted in <see cref="LinesWithUnknownMass"/> instead. A
    /// grand total that quietly treated an unknown weight as zero would be worse than no total at
    /// all, so the count travels with the figure everywhere it is displayed or exported.
    /// </remarks>
    public sealed class BomTotals
    {
        /// <summary>Initialises a new instance of the <see cref="BomTotals"/> class.</summary>
        /// <param name="lines">The lines to total.</param>
        public BomTotals(IReadOnlyList<BomLine> lines)
        {
            if (lines is null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            LineCount = lines.Count;
            TotalPieces = lines.Sum(l => l.Quantity);
            TotalLengthMm = lines.Where(l => l.TotalLengthMm.HasValue).Sum(l => l.TotalLengthMm!.Value);
            TotalMassKg = lines.Where(l => l.TotalMassKg.HasValue).Sum(l => l.TotalMassKg!.Value);
            LinesWithUnknownMass = lines.Count(l => !l.MassIsKnown);
            PiecesWithUnknownMass = lines.Where(l => !l.MassIsKnown).Sum(l => l.Quantity);
            NonConformingLines = lines.Count(l => l.IsCustom);
            UnverifiedSourceLines = lines.Count(l => !l.SourceDataVerified);
        }

        /// <summary>Gets the number of schedule lines.</summary>
        public int LineCount { get; }

        /// <summary>Gets the total number of pieces.</summary>
        public int TotalPieces { get; }

        /// <summary>Gets the total length of every piece that has one, in millimetres.</summary>
        public decimal TotalLengthMm { get; }

        /// <summary>Gets the total mass of every line whose mass is known, in kilograms.</summary>
        public decimal TotalMassKg { get; }

        /// <summary>Gets the total mass of every line whose mass is known, in metric tonnes.</summary>
        public decimal TotalMassTonnes => UnitConverter.FromKilograms(TotalMassKg, MassUnit.Tonne);

        /// <summary>Gets the number of lines that contributed no mass because none could be established.</summary>
        public int LinesWithUnknownMass { get; }

        /// <summary>Gets the number of pieces whose mass could not be established.</summary>
        public int PiecesWithUnknownMass { get; }

        /// <summary>Gets the number of lines that depart from their standard profile.</summary>
        public int NonConformingLines { get; }

        /// <summary>Gets the number of lines whose source data was not marked verified.</summary>
        public int UnverifiedSourceLines { get; }

        /// <summary>Gets a value indicating whether every line contributed a known mass.</summary>
        public bool IsComplete => LinesWithUnknownMass == 0;

        /// <summary>Expresses the total length in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted total length.</returns>
        public decimal TotalLengthIn(LengthUnit unit) => UnitConverter.FromMillimetres(TotalLengthMm, unit);

        /// <summary>Expresses the total mass in the requested unit.</summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The converted total mass.</returns>
        public decimal TotalMassIn(MassUnit unit) => UnitConverter.FromKilograms(TotalMassKg, unit);
    }
}
