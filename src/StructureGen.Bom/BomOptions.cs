using StructureGen.Core.Units;

namespace StructureGen.Bom
{
    /// <summary>Controls how a bill of materials is assembled.</summary>
    public sealed class BomOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether identical members are collapsed onto a single
        /// line carrying a quantity.
        /// </summary>
        public bool GroupIdenticalElements { get; set; } = true;

        /// <summary>
        /// Gets or sets the tolerance, in millimetres, within which two lengths count as the same
        /// for grouping. Zero requires an exact match.
        /// </summary>
        public decimal LengthToleranceMm { get; set; }

        /// <summary>Gets or sets a value indicating whether the layer takes part in grouping.</summary>
        public bool GroupByLayer { get; set; }

        /// <summary>Gets or sets a value indicating whether notes take part in grouping.</summary>
        public bool GroupByNotes { get; set; }

        /// <summary>Gets or sets the unit member lengths are reported in.</summary>
        public LengthUnit LengthUnit { get; set; } = LengthUnit.Metre;

        /// <summary>Gets or sets the unit masses are reported in alongside the always-present kilogram column.</summary>
        public MassUnit MassUnit { get; set; } = MassUnit.Kilogram;

        /// <summary>Gets or sets the title placed at the head of the schedule.</summary>
        public string Title { get; set; } = "BILL OF MATERIALS";

        /// <summary>Gets or sets an optional project name recorded on the schedule.</summary>
        public string? ProjectName { get; set; }

        /// <summary>Gets or sets an optional drawing reference recorded on the schedule.</summary>
        public string? DrawingReference { get; set; }

        /// <summary>Gets or sets a value indicating whether a subtotal row is emitted for each category.</summary>
        public bool IncludeCategorySubtotals { get; set; } = true;
    }
}
