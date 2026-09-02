using System;

namespace StructureGen.Core.Model
{
    /// <summary>The mass figures for one element, together with the provenance of each.</summary>
    public sealed class MassProperties
    {
        /// <summary>Initialises a new instance of the <see cref="MassProperties"/> class.</summary>
        /// <param name="source">How the mass was arrived at.</param>
        /// <param name="unitWeightKgPerM">Mass per metre, when the element is measured by length.</param>
        /// <param name="pieceMassKg">Mass of a single piece, when the element is counted.</param>
        /// <param name="totalMassKg">Mass of the whole quantity, or <see langword="null"/> when unknown.</param>
        /// <param name="crossSectionAreaMm2">The generated cross-sectional area, in square millimetres.</param>
        /// <param name="volumeMm3">The generated volume of one piece, in cubic millimetres.</param>
        /// <param name="explanation">A short traceable description of the calculation performed.</param>
        public MassProperties(
            WeightSource source,
            decimal? unitWeightKgPerM,
            decimal? pieceMassKg,
            decimal? totalMassKg,
            double crossSectionAreaMm2,
            double volumeMm3,
            string explanation)
        {
            Source = source;
            UnitWeightKgPerM = unitWeightKgPerM;
            PieceMassKg = pieceMassKg;
            TotalMassKg = totalMassKg;
            CrossSectionAreaMm2 = crossSectionAreaMm2;
            VolumeMm3 = volumeMm3;
            Explanation = explanation ?? throw new ArgumentNullException(nameof(explanation));
        }

        /// <summary>Gets how the mass was arrived at.</summary>
        public WeightSource Source { get; }

        /// <summary>Gets the mass per metre, when the element is measured by length.</summary>
        public decimal? UnitWeightKgPerM { get; }

        /// <summary>Gets the mass of a single piece, when the element is counted.</summary>
        public decimal? PieceMassKg { get; }

        /// <summary>Gets the mass of the whole quantity, or <see langword="null"/> when it is unknown.</summary>
        public decimal? TotalMassKg { get; }

        /// <summary>Gets the generated cross-sectional area, in square millimetres.</summary>
        public double CrossSectionAreaMm2 { get; }

        /// <summary>Gets the generated volume of one piece, in cubic millimetres.</summary>
        public double VolumeMm3 { get; }

        /// <summary>
        /// Gets a short description of exactly how the total was computed, for example
        /// <c>42.2 kg/m x 6.000 m x 4</c>. Shown in the bill of materials so a checker can
        /// reproduce every figure by hand.
        /// </summary>
        public string Explanation { get; }

        /// <summary>Gets a value indicating whether a usable total mass is available.</summary>
        public bool IsKnown => Source != WeightSource.Unavailable && TotalMassKg.HasValue;

        /// <summary>Creates a properties instance describing an unavailable mass.</summary>
        /// <param name="reason">Why the mass could not be established.</param>
        /// <param name="crossSectionAreaMm2">The generated cross-sectional area, in square millimetres.</param>
        /// <param name="volumeMm3">The generated volume of one piece, in cubic millimetres.</param>
        /// <returns>The properties.</returns>
        public static MassProperties Unavailable(string reason, double crossSectionAreaMm2 = 0d, double volumeMm3 = 0d) =>
            new(WeightSource.Unavailable, null, null, null, crossSectionAreaMm2, volumeMm3, reason);
    }
}
