namespace StructureGen.Core.Results
{
    /// <summary>Severity of a single <see cref="ValidationMessage"/>.</summary>
    public enum Severity
    {
        /// <summary>Informational; no action required.</summary>
        Info = 0,

        /// <summary>The operation can proceed but the result may not be what the user expects.</summary>
        Warning = 1,

        /// <summary>The operation must not proceed.</summary>
        Error = 2
    }
}
