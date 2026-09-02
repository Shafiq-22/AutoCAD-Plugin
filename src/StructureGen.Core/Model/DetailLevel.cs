namespace StructureGen.Core.Model
{
    /// <summary>
    /// How much geometric detail a generated element carries.
    /// </summary>
    /// <remarks>
    /// A drawing containing several thousand bolts becomes unusable if every one of them is
    /// modelled with fillets and chamfers, so the level is chosen per element and stored in its
    /// metadata. Changing it and re-running <c>SGUPDATE</c> rebuilds the geometry in place.
    /// </remarks>
    public enum DetailLevel
    {
        /// <summary>Sharp corners only: no root fillets, no toe radii, no chamfers. Cheapest to draw.</summary>
        Simplified = 0,

        /// <summary>Root fillets included. The default, and what most detailing work needs.</summary>
        Standard = 1,

        /// <summary>Root fillets, toe radii and head chamfers included. Heaviest to draw.</summary>
        Detailed = 2
    }
}
