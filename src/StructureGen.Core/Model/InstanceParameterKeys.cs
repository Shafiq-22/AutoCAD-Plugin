namespace StructureGen.Core.Model
{
    /// <summary>
    /// Keys used in a <see cref="ParameterSchema"/> for properties of an occurrence rather than of
    /// the cross-section.
    /// </summary>
    /// <remarks>
    /// They are prefixed so that they can never collide with a dimension key coming from a user's
    /// own database, however that database chooses to name its columns.
    /// </remarks>
    public static class InstanceParameterKeys
    {
        /// <summary>The member length.</summary>
        public const string Length = "__length";

        /// <summary>The number of pieces.</summary>
        public const string Quantity = "__quantity";

        /// <summary>The target layer.</summary>
        public const string Layer = "__layer";

        /// <summary>Free text notes carried to the bill of materials.</summary>
        public const string Notes = "__notes";

        /// <summary>The rotation of the profile about the extrusion axis.</summary>
        public const string Rotation = "__rotation";

        /// <summary>An offset applied along the world Z axis.</summary>
        public const string Elevation = "__elevation";

        /// <summary>Determines whether a key names an instance property rather than a dimension.</summary>
        /// <param name="key">The key to test.</param>
        /// <returns><see langword="true"/> when the key is an instance property key.</returns>
        public static bool IsInstanceKey(string? key) =>
            key is not null && key.StartsWith("__", System.StringComparison.Ordinal);
    }
}
