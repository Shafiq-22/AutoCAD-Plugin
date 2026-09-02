using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Geometry
{
    /// <summary>The complete generated geometry of one element, expressed without any CAD types.</summary>
    public sealed class ElementGeometry
    {
        /// <summary>Initialises a new instance of the <see cref="ElementGeometry"/> class.</summary>
        /// <param name="parts">The prismatic pieces making up the element.</param>
        /// <param name="primaryProfile">
        /// The cross-section used for the two-dimensional representation. Usually the profile of the
        /// first part, but for a bolt it is the shank rather than the head.
        /// </param>
        public ElementGeometry(IEnumerable<GeometryPart> parts, ProfileGeometry primaryProfile)
        {
            Parts = (parts ?? throw new ArgumentNullException(nameof(parts))).ToList();
            PrimaryProfile = primaryProfile ?? throw new ArgumentNullException(nameof(primaryProfile));

            if (Parts.Count == 0)
            {
                throw new ArgumentException("An element needs at least one part.", nameof(parts));
            }
        }

        /// <summary>Initialises a new instance of the <see cref="ElementGeometry"/> class from a single prism.</summary>
        /// <param name="profile">The cross-section.</param>
        /// <param name="length">The extrusion length in millimetres.</param>
        /// <param name="name">A short name for the part.</param>
        public ElementGeometry(ProfileGeometry profile, double length, string name = "body")
            : this(new[] { new GeometryPart(name, profile, 0d, length) }, profile)
        {
        }

        /// <summary>Gets the prismatic pieces making up the element.</summary>
        public IReadOnlyList<GeometryPart> Parts { get; }

        /// <summary>Gets the cross-section used for the two-dimensional representation.</summary>
        public ProfileGeometry PrimaryProfile { get; }

        /// <summary>Gets the total volume of one piece, in cubic millimetres.</summary>
        public double VolumeMm3 => Parts.Sum(p => p.VolumeMm3);

        /// <summary>Gets the overall extent along the local Z axis, in millimetres.</summary>
        public double OverallLengthMm => Parts.Max(p => p.EndOffset) - Parts.Min(p => p.StartOffset);
    }
}
