using System;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// Where and how one instance of an element sits in the drawing.
    /// </summary>
    /// <remarks>
    /// The extrusion axis is <see cref="AxisDirection"/>; the profile is drawn in the plane normal
    /// to it. <see cref="RotationDegrees"/> spins the profile about that axis, which is how a beam is
    /// laid on its side without touching any standardised cross-sectional dimension.
    /// </remarks>
    public sealed class Placement
    {
        /// <summary>Initialises a new instance of the <see cref="Placement"/> class.</summary>
        /// <param name="origin">The insertion point, in drawing millimetres.</param>
        /// <param name="axisDirection">The direction the member is extruded along.</param>
        /// <param name="rotationDegrees">Rotation of the profile about the extrusion axis.</param>
        /// <param name="elevation">An additional offset applied along the world Z axis.</param>
        public Placement(Vec3 origin, Vec3 axisDirection, double rotationDegrees = 0d, double elevation = 0d)
        {
            Origin = origin;
            AxisDirection = axisDirection.Magnitude <= 1e-9 ? Vec3.UnitX : axisDirection.Normalised();
            RotationDegrees = rotationDegrees;
            Elevation = elevation;
        }

        /// <summary>Gets the default placement: world origin, extruded along X, unrotated.</summary>
        public static Placement Default => new(Vec3.Origin, Vec3.UnitX);

        /// <summary>Gets the insertion point, in drawing millimetres.</summary>
        public Vec3 Origin { get; }

        /// <summary>Gets the unit direction the member is extruded along.</summary>
        public Vec3 AxisDirection { get; }

        /// <summary>Gets the rotation of the profile about the extrusion axis, in degrees.</summary>
        public double RotationDegrees { get; }

        /// <summary>Gets an additional offset applied along the world Z axis.</summary>
        public double Elevation { get; }

        /// <summary>Gets the effective insertion point, that is the origin raised by the elevation.</summary>
        public Vec3 EffectiveOrigin => new(Origin.X, Origin.Y, Origin.Z + Elevation);

        /// <summary>
        /// Builds a placement from a start point and an end point, deriving both the direction and
        /// the true geometric length from the two picks.
        /// </summary>
        /// <param name="start">The start point.</param>
        /// <param name="end">The end point.</param>
        /// <param name="rotationDegrees">Rotation of the profile about the derived axis.</param>
        /// <returns>The placement and the measured length in millimetres.</returns>
        /// <exception cref="ArgumentException">The two points coincide, so no direction exists.</exception>
        public static (Placement Placement, decimal LengthMm) FromEndPoints(Vec3 start, Vec3 end, double rotationDegrees = 0d)
        {
            Vec3 delta = end - start;
            double length = delta.Magnitude;
            if (length <= 1e-9)
            {
                throw new ArgumentException("The start and end points coincide, so the member direction and length are undefined.", nameof(end));
            }

            return (new Placement(start, delta, rotationDegrees), (decimal)length);
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"origin {Origin}, axis {AxisDirection}, rotation {RotationDegrees:0.##} deg, elevation {Elevation:0.##}";
    }
}
