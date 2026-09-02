namespace StructureGen.Core.Model
{
    /// <summary>
    /// How a mass figure was arrived at.
    /// </summary>
    /// <remarks>
    /// Carried through to the bill of materials and the exported workbook so that every number
    /// on a weight schedule can be traced back to either a published table or an explicit
    /// density, and so that a missing figure is visibly missing rather than quietly zero.
    /// </remarks>
    public enum WeightSource
    {
        /// <summary>Taken from the mass-per-metre published in the database record.</summary>
        UnitWeightTable = 0,

        /// <summary>Taken from the mass-per-piece published in the database record.</summary>
        PieceMassTable = 1,

        /// <summary>Computed from the generated cross-sectional area and a density supplied by the database.</summary>
        ComputedFromDensity = 2,

        /// <summary>
        /// Neither a tabulated mass nor a density was available. The mass is reported as unknown;
        /// it is never guessed and never treated as zero in a total.
        /// </summary>
        Unavailable = 3
    }
}
