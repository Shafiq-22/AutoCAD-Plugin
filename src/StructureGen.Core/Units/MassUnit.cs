namespace StructureGen.Core.Units
{
    /// <summary>Units of mass understood by StructureGen.</summary>
    /// <remarks>Kilograms are the canonical internal unit.</remarks>
    public enum MassUnit
    {
        /// <summary>Gram.</summary>
        Gram = 0,

        /// <summary>Kilogram (the canonical internal mass unit).</summary>
        Kilogram = 1,

        /// <summary>Metric tonne (1000 kg).</summary>
        Tonne = 2,

        /// <summary>International avoirdupois pound (exactly 0.45359237 kg).</summary>
        Pound = 3
    }
}
