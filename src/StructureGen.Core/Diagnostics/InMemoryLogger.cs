using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Diagnostics
{
    /// <summary>A logger that keeps entries in memory. Intended for tests and for diagnostics dialogs.</summary>
    public sealed class InMemoryLogger : ILogger
    {
        private readonly object _gate = new();
        private readonly List<LogEntry> _entries = new();

        /// <summary>Gets a snapshot of everything logged so far.</summary>
        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList();
                }
            }
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            lock (_gate)
            {
                _entries.Add(new LogEntry(level, message, exception));
            }
        }

        /// <summary>A single captured log entry.</summary>
        /// <param name="Level">The severity.</param>
        /// <param name="Message">The message.</param>
        /// <param name="Exception">The associated exception, if any.</param>
        public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
    }
}
