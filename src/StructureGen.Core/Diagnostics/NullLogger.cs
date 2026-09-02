using System;

namespace StructureGen.Core.Diagnostics
{
    /// <summary>A logger that discards everything written to it.</summary>
    public sealed class NullLogger : ILogger
    {
        /// <summary>Gets the shared instance.</summary>
        public static NullLogger Instance { get; } = new();

        private NullLogger()
        {
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // Intentionally does nothing.
        }
    }
}
