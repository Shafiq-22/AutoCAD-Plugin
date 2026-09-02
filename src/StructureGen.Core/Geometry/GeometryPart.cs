using System;

namespace StructureGen.Core.Geometry
{
    /// <summary>
    /// One prismatic piece of an element: a cross-section extruded along the local Z axis between
    /// two stations.
    /// </summary>
    /// <remarks>
    /// Most elements are a single part. Fasteners are not: a hexagon bolt is a hexagonal head over
    /// one range of Z and a cylindrical shank over another, and describing it as a list of prisms
    /// keeps it inside the same generate-and-extrude machinery as a beam instead of needing a
    /// special case in the AutoCAD layer.
    /// </remarks>
    public sealed class GeometryPart
    {
        /// <summary>Initialises a new instance of the <see cref="GeometryPart"/> class.</summary>
        /// <param name="name">A short name such as <c>head</c> or <c>shank</c>, used in diagnostics.</param>
        /// <param name="profile">The cross-section, in the local XY plane.</param>
        /// <param name="startOffset">Where the part begins along the local Z axis, in millimetres.</param>
        /// <param name="length">The extrusion length along the local Z axis, in millimetres.</param>
        public GeometryPart(string name, ProfileGeometry profile, double startOffset, double length)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            StartOffset = startOffset;

            if (length <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "An extruded part needs a length greater than zero.");
            }

            Length = length;
        }

        /// <summary>Gets the short name, used in diagnostics.</summary>
        public string Name { get; }

        /// <summary>Gets the cross-section, in the local XY plane.</summary>
        public ProfileGeometry Profile { get; }

        /// <summary>Gets where the part begins along the local Z axis, in millimetres.</summary>
        public double StartOffset { get; }

        /// <summary>Gets the extrusion length along the local Z axis, in millimetres.</summary>
        public double Length { get; }

        /// <summary>Gets where the part ends along the local Z axis, in millimetres.</summary>
        public double EndOffset => StartOffset + Length;

        /// <summary>Gets the volume of the part in cubic millimetres.</summary>
        public double VolumeMm3 => Profile.AreaMm2 * Length;

        /// <inheritdoc />
        public override string ToString() => $"{Name}: {Length:0.###} mm from {StartOffset:0.###} mm";
    }
}
