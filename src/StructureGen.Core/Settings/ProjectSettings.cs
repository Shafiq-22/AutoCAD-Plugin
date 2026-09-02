using StructureGen.Core.Model;
using StructureGen.Core.Units;

namespace StructureGen.Core.Settings
{
    /// <summary>
    /// The per-user, per-project configuration of StructureGen.
    /// </summary>
    /// <remarks>
    /// Every path is resolved at run time. Nothing here is compiled into the assembly, so the
    /// plugin can be installed and used without editing or rebuilding any source.
    /// </remarks>
    public sealed class ProjectSettings
    {
        /// <summary>Gets or sets the unit lengths are displayed and entered in.</summary>
        public LengthUnit DisplayLengthUnit { get; set; } = LengthUnit.Millimetre;

        /// <summary>Gets or sets the unit member lengths are displayed in on the bill of materials.</summary>
        public LengthUnit BomLengthUnit { get; set; } = LengthUnit.Metre;

        /// <summary>Gets or sets the unit masses are displayed in.</summary>
        public MassUnit DisplayMassUnit { get; set; } = MassUnit.Kilogram;

        /// <summary>
        /// Gets or sets the path of the structural database file. Relative paths are resolved
        /// against the plugin's own data folder, so the shipped demonstration database is found
        /// without configuration.
        /// </summary>
        public string DatabasePath { get; set; } = "structural-sections.json";

        /// <summary>Gets or sets the default level of geometric detail for newly added elements.</summary>
        public DetailLevel DefaultDetailLevel { get; set; } = DetailLevel.Standard;

        /// <summary>Gets or sets the default representation for newly added elements.</summary>
        public RepresentationMode DefaultRepresentation { get; set; } = RepresentationMode.Solid3D;

        /// <summary>Gets or sets the prefix used for the layers StructureGen creates.</summary>
        public string LayerPrefix { get; set; } = "SG-";

        /// <summary>
        /// Gets or sets a value indicating whether identical members are collapsed onto one
        /// bill-of-materials line carrying a quantity.
        /// </summary>
        public bool GroupIdenticalElements { get; set; } = true;

        /// <summary>
        /// Gets or sets the tolerance, in millimetres, within which two member lengths count as
        /// the same for grouping. Zero means lengths must match exactly.
        /// </summary>
        public decimal LengthGroupingToleranceMm { get; set; } = 0m;

        /// <summary>Gets or sets a value indicating whether the layer name takes part in grouping.</summary>
        public bool GroupByLayer { get; set; }

        /// <summary>Gets or sets the text height used for generated bill-of-materials tables, in drawing units.</summary>
        public double BomTextHeight { get; set; } = 2.5d;

        /// <summary>Gets or sets the text style applied to generated bill-of-materials tables.</summary>
        public string BomTextStyle { get; set; } = "Standard";

        /// <summary>Gets or sets the folder last used for an Excel export.</summary>
        public string? LastExportDirectory { get; set; }

        /// <summary>Gets or sets a value indicating whether verbose diagnostics are written to the log.</summary>
        public bool VerboseLogging { get; set; }

        /// <summary>Creates a copy of these settings.</summary>
        /// <returns>A new instance holding the same values.</returns>
        public ProjectSettings Clone() => new()
        {
            DisplayLengthUnit = DisplayLengthUnit,
            BomLengthUnit = BomLengthUnit,
            DisplayMassUnit = DisplayMassUnit,
            DatabasePath = DatabasePath,
            DefaultDetailLevel = DefaultDetailLevel,
            DefaultRepresentation = DefaultRepresentation,
            LayerPrefix = LayerPrefix,
            GroupIdenticalElements = GroupIdenticalElements,
            LengthGroupingToleranceMm = LengthGroupingToleranceMm,
            GroupByLayer = GroupByLayer,
            BomTextHeight = BomTextHeight,
            BomTextStyle = BomTextStyle,
            LastExportDirectory = LastExportDirectory,
            VerboseLogging = VerboseLogging
        };
    }
}
