namespace StructureGen.Core.Model
{
    /// <summary>
    /// Whether the user may change a parameter, and what changing it means.
    /// </summary>
    /// <remarks>
    /// This is the mechanism that stops a standard profile being silently turned into something
    /// that no longer exists in any catalogue. It is enforced twice: the user interface disables
    /// the control, and the validator rejects the change even if the definition is constructed
    /// programmatically.
    /// </remarks>
    public enum ParameterMutability
    {
        /// <summary>
        /// A standardised cross-sectional dimension. Read-only unless the element is explicitly
        /// switched into custom mode, which flags it as non-conforming everywhere it appears.
        /// </summary>
        StandardLocked = 0,

        /// <summary>
        /// A dimension the standard genuinely leaves open, such as bolt length within the range
        /// the standard publishes, or the cut size of a plate.
        /// </summary>
        Adjustable = 1,

        /// <summary>
        /// A property of this particular occurrence rather than of the profile: member length,
        /// quantity, layer, orientation, notes.
        /// </summary>
        Instance = 2
    }
}
