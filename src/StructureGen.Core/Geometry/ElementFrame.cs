using System;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// Where an element's local coordinate system sits in the world, in millimetres.
    /// </summary>
    /// <remarks>
    /// In the local frame the cross-section lies in XY and the member runs along positive Z. A
    /// <see cref="Placement"/> describes a frame the way a user thinks of one; this is the same frame
    /// spelled out as three axes, which is also what can be read back from an object already in the
    /// drawing after it has been moved, rotated or mirrored by hand.
    /// </remarks>
    public sealed class ElementFrame
    {
        /// <summary>Initialises a new instance of the <see cref="ElementFrame"/> class.</summary>
        /// <param name="origin">The world position of the local origin, in millimetres.</param>
        /// <param name="xAxis">The world direction of the local X axis.</param>
        /// <param name="yAxis">The world direction of the local Y axis.</param>
        /// <param name="zAxis">The world direction of the local Z axis, along the member.</param>
        /// <remarks>
        /// The axes are normalised but not otherwise corrected, so a mirrored object keeps its
        /// left-handed frame and is drawn mirrored.
        /// </remarks>
        public ElementFrame(Vec3 origin, Vec3 xAxis, Vec3 yAxis, Vec3 zAxis)
        {
            if (xAxis.Magnitude <= 1e-12 || yAxis.Magnitude <= 1e-12 || zAxis.Magnitude <= 1e-12)
            {
                throw new ArgumentException("Every axis of an element frame needs a direction.");
            }

            Origin = origin;
            XAxis = xAxis.Normalised();
            YAxis = yAxis.Normalised();
            ZAxis = zAxis.Normalised();
        }

        /// <summary>Gets the world position of the local origin, in millimetres.</summary>
        public Vec3 Origin { get; }

        /// <summary>Gets the world direction of the local X axis.</summary>
        public Vec3 XAxis { get; }

        /// <summary>Gets the world direction of the local Y axis.</summary>
        public Vec3 YAxis { get; }

        /// <summary>Gets the world direction of the local Z axis, along the member.</summary>
        public Vec3 ZAxis { get; }

        /// <summary>Gets a frame that coincides with the world.</summary>
        public static ElementFrame World => new(Vec3.Origin, Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);

        /// <summary>
        /// Builds the frame a placement describes, using exactly the convention the AutoCAD layer
        /// uses to position generated geometry.
        /// </summary>
        /// <param name="placement">The placement.</param>
        /// <returns>The frame.</returns>
        /// <remarks>
        /// The local X axis is taken from a world reference chosen to avoid being parallel to the
        /// member, so a horizontal beam stands upright (its section height vertical) and a vertical
        /// column gets a stable, repeatable orientation.
        /// </remarks>
        public static ElementFrame FromPlacement(Placement placement)
        {
            if (placement is null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            Vec3 z = placement.AxisDirection.Normalised();
            Vec3 reference = Math.Abs(z.Z) > 0.9999d ? Vec3.UnitX : Vec3.UnitZ;
            Vec3 x = Vec3.Cross(reference, z).Normalised();
            Vec3 y = Vec3.Cross(z, x).Normalised();

            if (Math.Abs(placement.RotationDegrees) > 1e-9)
            {
                double radians = placement.RotationDegrees * Math.PI / 180d;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);

                // A rotation about the member axis turns X towards Y.
                Vec3 rotatedX = (x * cos) + (y * sin);
                Vec3 rotatedY = (y * cos) - (x * sin);
                x = rotatedX;
                y = rotatedY;
            }

            return new ElementFrame(placement.EffectiveOrigin, x, y, z);
        }

        /// <summary>Carries a point from the local frame into the world.</summary>
        /// <param name="x">The local X coordinate.</param>
        /// <param name="y">The local Y coordinate.</param>
        /// <param name="z">The local Z coordinate, along the member.</param>
        /// <returns>The world point.</returns>
        public Vec3 ToWorld(double x, double y, double z) =>
            Origin + (XAxis * x) + (YAxis * y) + (ZAxis * z);

        /// <inheritdoc />
        public override string ToString() => $"origin {Origin}, x {XAxis}, y {YAxis}, z {ZAxis}";
    }
}
