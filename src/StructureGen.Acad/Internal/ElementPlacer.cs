using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;

namespace StructureGen.Acad.Internal
{
    /// <summary>Decides where each piece of an item actually goes.</summary>
    /// <remarks>
    /// A schedule line says "eight of these"; the drawing needs eight positions. Where the user
    /// picked positions they are honoured exactly. Where they picked one, or none, the remaining
    /// pieces are laid out side by side rather than stacked on top of one another, because eight
    /// coincident beams look like one beam and are a menace to select.
    /// </remarks>
    internal static class ElementPlacer
    {
        /// <summary>The spacing used when the caller supplies none, as a multiple of the profile width.</summary>
        private const double DefaultSpacingFactor = 1.5d;

        /// <summary>Produces one placement per piece.</summary>
        /// <param name="definition">The item being placed.</param>
        /// <param name="geometry">Its geometry, used to derive a sensible default spacing.</param>
        /// <param name="requestedSpacingMm">
        /// The spacing to use, in millimetres. Zero asks for a spacing derived from the profile, and
        /// a negative value stacks the pieces at one point.
        /// </param>
        /// <returns>Exactly <see cref="ElementDefinition.Quantity"/> placements.</returns>
        public static IReadOnlyList<Placement> Expand(
            ElementDefinition definition, ElementGeometry geometry, double requestedSpacingMm)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int quantity = Math.Max(1, definition.Quantity);
            List<Placement> placements = new(quantity);

            if (definition.Placements.Count >= quantity)
            {
                for (int index = 0; index < quantity; index++)
                {
                    placements.Add(definition.Placements[index]);
                }

                return placements;
            }

            Placement seed = definition.Placements.Count > 0 ? definition.Placements[0] : Placement.Default;
            placements.Add(seed);

            if (quantity == 1)
            {
                return placements;
            }

            if (requestedSpacingMm < 0d)
            {
                for (int index = 1; index < quantity; index++)
                {
                    placements.Add(seed);
                }

                return placements;
            }

            double spacing = requestedSpacingMm > 0d
                ? requestedSpacingMm
                : Math.Max(10d, geometry.PrimaryProfile.WidthMm * DefaultSpacingFactor);

            // Copies are offset perpendicular to the member, so a row of beams sits side by side
            // rather than end to end.
            Vec3 axis = seed.AxisDirection;
            Vec3 reference = Math.Abs(axis.Z) > 0.9999d ? Vec3.UnitX : Vec3.UnitZ;
            Vec3 offsetDirection = Vec3.Cross(reference, axis);

            offsetDirection = offsetDirection.Magnitude <= 1e-9
                ? Vec3.UnitY
                : offsetDirection.Normalised();

            for (int index = 1; index < quantity; index++)
            {
                Vec3 origin = seed.Origin + (offsetDirection * (spacing * index));
                placements.Add(new Placement(origin, axis, seed.RotationDegrees, seed.Elevation));
            }

            return placements;
        }
    }
}
