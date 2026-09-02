namespace StructureGen.Core.Units
{
    /// <summary>Units of length understood by StructureGen.</summary>
    /// <remarks>
    /// Millimetres are the canonical internal unit. Every other member exists only for
    /// data import and for presentation.
    /// </remarks>
    public enum LengthUnit
    {
        /// <summary>Millimetre (the canonical internal length unit).</summary>
        Millimetre = 0,

        /// <summary>Centimetre.</summary>
        Centimetre = 1,

        /// <summary>Metre.</summary>
        Metre = 2,

        /// <summary>International inch (exactly 25.4 mm).</summary>
        Inch = 3,

        /// <summary>International foot (exactly 304.8 mm).</summary>
        Foot = 4
    }
}
