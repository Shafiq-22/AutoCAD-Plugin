namespace StructureGen.Core.Model
{
    /// <summary>
    /// The stable identifiers used for the built-in element categories.
    /// </summary>
    /// <remarks>
    /// These strings appear in the JSON database and in the metadata written onto AutoCAD
    /// objects, so they are part of the persisted contract and must not be renamed. The
    /// database may reference a category that is not listed here; it is then handled by the
    /// generic custom-profile generator.
    /// </remarks>
    public static class ElementCategory
    {
        /// <summary>Rolled I sections (IPE, UB, W and similar).</summary>
        public const string ISection = "i-section";

        /// <summary>Wide-flange H sections (HEA, HEB, UC and similar).</summary>
        public const string HSection = "h-section";

        /// <summary>Parallel- or taper-flange channels (UPN, PFC, C).</summary>
        public const string Channel = "channel";

        /// <summary>Equal and unequal leg angles.</summary>
        public const string Angle = "angle";

        /// <summary>T sections, whether rolled or split from an I section.</summary>
        public const string TSection = "t-section";

        /// <summary>Circular hollow sections and pipes.</summary>
        public const string Pipe = "pipe";

        /// <summary>Rectangular hollow sections.</summary>
        public const string RectangularHollowSection = "rhs";

        /// <summary>Square hollow sections.</summary>
        public const string SquareHollowSection = "shs";

        /// <summary>Flat plates.</summary>
        public const string Plate = "plate";

        /// <summary>Flat bars.</summary>
        public const string FlatBar = "flat-bar";

        /// <summary>Round bars.</summary>
        public const string RoundBar = "round-bar";

        /// <summary>Hexagon head bolts.</summary>
        public const string Bolt = "bolt";

        /// <summary>Hexagon nuts.</summary>
        public const string Nut = "nut";

        /// <summary>Plain washers.</summary>
        public const string Washer = "washer";

        /// <summary>A user-defined profile described entirely by its database record.</summary>
        public const string Custom = "custom";
    }
}
