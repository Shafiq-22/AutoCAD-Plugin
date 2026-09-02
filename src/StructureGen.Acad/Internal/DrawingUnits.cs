using Autodesk.AutoCAD.DatabaseServices;

namespace StructureGen.Acad.Internal
{
    /// <summary>
    /// Reconciles StructureGen's internal millimetres with whatever unit the drawing is in.
    /// </summary>
    /// <remarks>
    /// Getting this wrong is the classic way to produce a beam a thousand times too long, so the
    /// conversion happens in exactly one place and every coordinate written into the drawing passes
    /// through it. A drawing whose insertion units are unset is treated as millimetres, and the user
    /// is told once rather than left to wonder.
    /// </remarks>
    internal static class DrawingUnits
    {
        /// <summary>
        /// Returns how many drawing units one millimetre is worth.
        /// </summary>
        /// <param name="units">The drawing's insertion units.</param>
        /// <returns>The scale to apply to every internal millimetre value.</returns>
        public static double DrawingUnitsPerMillimetre(UnitsValue units) => units switch
        {
            UnitsValue.Millimeters => 1d,
            UnitsValue.Centimeters => 0.1d,
            UnitsValue.Decimeters => 0.01d,
            UnitsValue.Meters => 0.001d,
            UnitsValue.Inches => 1d / 25.4d,
            UnitsValue.Feet => 1d / 304.8d,
            _ => 1d
        };

        /// <summary>Determines whether the drawing declares a usable insertion unit.</summary>
        /// <param name="units">The drawing's insertion units.</param>
        /// <returns><see langword="false"/> when the drawing is unitless or uses an unsupported unit.</returns>
        public static bool IsSupported(UnitsValue units) => units switch
        {
            UnitsValue.Millimeters or UnitsValue.Centimeters or UnitsValue.Decimeters or
            UnitsValue.Meters or UnitsValue.Inches or UnitsValue.Feet => true,
            _ => false
        };

        /// <summary>Maps the drawing's insertion units onto a StructureGen length unit.</summary>
        /// <param name="units">The drawing's insertion units.</param>
        /// <returns>The equivalent unit, or <see langword="null"/> when the drawing is unitless.</returns>
        public static Core.Units.LengthUnit? ToLengthUnit(UnitsValue units) => units switch
        {
            UnitsValue.Millimeters => Core.Units.LengthUnit.Millimetre,
            UnitsValue.Centimeters => Core.Units.LengthUnit.Centimetre,
            UnitsValue.Meters => Core.Units.LengthUnit.Metre,
            UnitsValue.Inches => Core.Units.LengthUnit.Inch,
            UnitsValue.Feet => Core.Units.LengthUnit.Foot,
            _ => null
        };
    }
}
