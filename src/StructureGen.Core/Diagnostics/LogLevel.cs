namespace StructureGen.Core.Diagnostics
{
    /// <summary>Severity of a log entry.</summary>
    public enum LogLevel
    {
        /// <summary>Fine-grained diagnostic detail, off by default.</summary>
        Debug = 0,

        /// <summary>Normal progress information.</summary>
        Information = 1,

        /// <summary>Something unexpected that did not stop the operation.</summary>
        Warning = 2,

        /// <summary>An operation failed.</summary>
        Error = 3
    }
}
