namespace StructureGen.Core.Model
{
    /// <summary>Additional dimension keys that only fasteners use.</summary>
    public static class FastenerDimensionKeys
    {
        /// <summary>
        /// The thread minor diameter, <c>d3</c>. Optional. When present, the threaded portion of a
        /// bolt is drawn at this diameter at the detailed level, which is the usual detailing
        /// convention. When absent the shank is drawn at full diameter and no thread is implied.
        /// </summary>
        public const string ThreadMinorDiameter = "d3";

        /// <summary>
        /// The unthreaded shank length, <c>ls</c>. Optional. When present the shank is split into a
        /// plain portion of this length and a threaded remainder.
        /// </summary>
        public const string UnthreadedLength = "ls";

        /// <summary>The smallest bolt length the standard publishes for this diameter, <c>lmin</c>. Optional.</summary>
        public const string MinimumBoltLength = "lmin";

        /// <summary>The largest bolt length the standard publishes for this diameter, <c>lmax</c>. Optional.</summary>
        public const string MaximumBoltLength = "lmax";
    }
}
