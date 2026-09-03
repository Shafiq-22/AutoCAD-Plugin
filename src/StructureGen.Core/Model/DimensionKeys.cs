namespace StructureGen.Core.Model
{
    /// <summary>
    /// The canonical dimension keys used inside <see cref="SectionRecord.Dimensions"/>.
    /// </summary>
    /// <remarks>
    /// Every value stored under these keys is in millimetres. Keeping the vocabulary small and
    /// explicit is what allows a single generator to serve every standard: an IPE record from EN
    /// and a W record from AISC both describe their web thickness as <see cref="WebThickness"/>,
    /// and the import mapping layer is responsible for translating a source column name onto one
    /// of these keys. The full vocabulary is documented in <c>docs/DATABASE.md</c>.
    /// </remarks>
    public static class DimensionKeys
    {
        /// <summary>Overall section depth, <c>h</c>.</summary>
        public const string Height = "h";

        /// <summary>Overall section width or flange width, <c>b</c>.</summary>
        public const string Width = "b";

        /// <summary>Web thickness, <c>tw</c>.</summary>
        public const string WebThickness = "tw";

        /// <summary>Flange thickness, <c>tf</c>.</summary>
        public const string FlangeThickness = "tf";

        /// <summary>Root fillet radius, <c>r</c>.</summary>
        public const string RootRadius = "r";

        /// <summary>Toe or flange-tip radius, <c>r2</c>.</summary>
        public const string ToeRadius = "r2";

        /// <summary>Outside diameter of a circular section, <c>d</c>.</summary>
        public const string OutsideDiameter = "d";

        /// <summary>Wall thickness of a hollow section, <c>t</c>.</summary>
        public const string WallThickness = "t";

        /// <summary>Outside corner radius of a rectangular or square hollow section, <c>ro</c>.</summary>
        public const string OuterCornerRadius = "ro";

        /// <summary>
        /// The inside corner radius of a hollow section, in millimetres.
        /// </summary>
        /// <remarks>
        /// Usually left out, in which case it is taken as the outside radius less the wall
        /// thickness, which is the shape a tube of uniform wall actually has. It is worth stating
        /// where a standard defines the corner differently: EN 10210 gives a hot finished section
        /// an outside radius of 1.5t against an inside radius of 1.0t, and using the uniform wall
        /// figure of 0.5t instead misses the area those tables publish by up to three per cent on
        /// a thick walled section.
        /// </remarks>
        public const string InnerCornerRadius = "ri";

        /// <summary>Leg length A of an angle, <c>a</c>.</summary>
        public const string LegA = "a";

        /// <summary>Leg length B of an angle, <c>b2</c>.</summary>
        public const string LegB = "b2";

        /// <summary>Leg or plate thickness, <c>tt</c>.</summary>
        public const string Thickness = "tt";

        /// <summary>Plate or bar width, <c>w</c>.</summary>
        public const string PlateWidth = "w";

        /// <summary>Nominal thread diameter of a fastener, <c>dth</c>.</summary>
        public const string ThreadDiameter = "dth";

        /// <summary>Width across flats of a hexagon head or nut, <c>s</c>.</summary>
        public const string AcrossFlats = "s";

        /// <summary>Head height of a bolt, <c>k</c>.</summary>
        public const string HeadHeight = "k";

        /// <summary>Nut height, <c>m</c>.</summary>
        public const string NutHeight = "m";

        /// <summary>Shank length of a bolt, <c>l</c>. Adjustable where the standard offers a range.</summary>
        public const string BoltLength = "l";

        /// <summary>Washer inside diameter, <c>d1</c>.</summary>
        public const string WasherInsideDiameter = "d1";

        /// <summary>Washer outside diameter, <c>d2</c>.</summary>
        public const string WasherOutsideDiameter = "d2";

        /// <summary>Washer thickness, <c>hw</c>.</summary>
        public const string WasherThickness = "hw";

        /// <summary>Cross-sectional area in square millimetres, <c>A</c>. Informational.</summary>
        public const string Area = "A";
    }
}
