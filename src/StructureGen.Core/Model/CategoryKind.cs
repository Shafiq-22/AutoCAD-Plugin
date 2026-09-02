namespace StructureGen.Core.Model
{
    /// <summary>How an element behaves parametrically, which decides what the user may edit.</summary>
    public enum CategoryKind
    {
        /// <summary>
        /// A prismatic member extruded along a length: beams, channels, angles, pipes and bars.
        /// Length is a free instance parameter; the cross section comes from the standard.
        /// </summary>
        Linear = 0,

        /// <summary>
        /// A planar element described by length, width and thickness, such as a plate.
        /// All three are user parameters because plates are cut to order, not rolled to a standard.
        /// </summary>
        Planar = 1,

        /// <summary>
        /// A discrete manufactured item counted in pieces rather than measured in metres:
        /// bolts, nuts and washers. Mass is per piece, not per metre.
        /// </summary>
        Fastener = 2
    }
}
