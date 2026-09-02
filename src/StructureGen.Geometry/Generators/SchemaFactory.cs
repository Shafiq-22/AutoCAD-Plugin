using System.Collections.Generic;
using StructureGen.Core.Model;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Helpers that assemble the repetitive parts of a <see cref="ParameterSchema"/>.</summary>
    internal static class SchemaFactory
    {
        /// <summary>Creates a descriptor for a dimension fixed by the standard.</summary>
        /// <param name="key">The dimension key.</param>
        /// <param name="displayName">The label shown to the user.</param>
        /// <param name="required">Whether geometry generation fails without it.</param>
        /// <param name="description">Optional explanatory text.</param>
        /// <returns>The descriptor.</returns>
        public static ParameterDescriptor Locked(
            string key, string displayName, bool required = true, string? description = null) =>
            new(key, displayName, ParameterMutability.StandardLocked, required, 0m, null,
                description ?? "Fixed by the selected standard. Switch the item to custom mode to change it.");

        /// <summary>Creates a descriptor for a dimension the standard leaves open.</summary>
        /// <param name="key">The dimension key.</param>
        /// <param name="displayName">The label shown to the user.</param>
        /// <param name="required">Whether geometry generation fails without it.</param>
        /// <param name="minimum">Optional inclusive lower bound in millimetres.</param>
        /// <param name="maximum">Optional inclusive upper bound in millimetres.</param>
        /// <param name="description">Optional explanatory text.</param>
        /// <returns>The descriptor.</returns>
        public static ParameterDescriptor Adjustable(
            string key,
            string displayName,
            bool required = true,
            decimal? minimum = null,
            decimal? maximum = null,
            string? description = null) =>
            new(key, displayName, ParameterMutability.Adjustable, required, minimum ?? 0m, maximum, description);

        /// <summary>
        /// Appends the parameters every element has: length where the category has one, quantity,
        /// orientation, layer and notes.
        /// </summary>
        /// <param name="parameters">The list to append to.</param>
        /// <param name="descriptor">The category descriptor.</param>
        public static void AddInstanceParameters(List<ParameterDescriptor> parameters, CategoryDescriptor descriptor)
        {
            if (descriptor.LengthIsUserParameter)
            {
                parameters.Add(new ParameterDescriptor(
                    InstanceParameterKeys.Length,
                    "Length",
                    ParameterMutability.Instance,
                    true,
                    0m,
                    1_000_000m,
                    "The cut length of this member. Changing it never alters the cross-section."));
            }

            parameters.Add(new ParameterDescriptor(
                InstanceParameterKeys.Quantity, "Quantity", ParameterMutability.Instance, true, 1m, 100_000m,
                "How many identical pieces this line represents."));

            parameters.Add(new ParameterDescriptor(
                InstanceParameterKeys.Rotation, "Rotation", ParameterMutability.Instance, false, null, null,
                "Rotation of the profile about the member axis, in degrees."));

            parameters.Add(new ParameterDescriptor(
                InstanceParameterKeys.Elevation, "Elevation", ParameterMutability.Instance, false, null, null,
                "Offset applied along the world Z axis at insertion."));

            parameters.Add(new ParameterDescriptor(
                InstanceParameterKeys.Layer, "Layer", ParameterMutability.Instance, false, null, null,
                "Target layer. Leave empty to use the automatic layer for this category."));

            parameters.Add(new ParameterDescriptor(
                InstanceParameterKeys.Notes, "Notes", ParameterMutability.Instance, false, null, null,
                "Free text carried through to the bill of materials."));
        }
    }
}
