using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Results
{
    /// <summary>
    /// An accumulating collection of validation findings.
    /// </summary>
    /// <remarks>
    /// Expected failures — a malformed database row, a negative length, a missing density —
    /// are reported through this type rather than by throwing. Exceptions are reserved for
    /// genuine defects and for failures raised by the platform.
    /// </remarks>
    public sealed class ValidationReport
    {
        private readonly List<ValidationMessage> _messages = new();

        /// <summary>Gets every message recorded so far, in the order it was added.</summary>
        public IReadOnlyList<ValidationMessage> Messages => _messages;

        /// <summary>Gets a value indicating whether at least one <see cref="Severity.Error"/> was recorded.</summary>
        public bool HasErrors => _messages.Any(m => m.Severity == Severity.Error);

        /// <summary>Gets a value indicating whether at least one <see cref="Severity.Warning"/> was recorded.</summary>
        public bool HasWarnings => _messages.Any(m => m.Severity == Severity.Warning);

        /// <summary>Gets a value indicating whether the validated subject may be used.</summary>
        public bool IsValid => !HasErrors;

        /// <summary>Gets the number of errors recorded.</summary>
        public int ErrorCount => _messages.Count(m => m.Severity == Severity.Error);

        /// <summary>Gets the number of warnings recorded.</summary>
        public int WarningCount => _messages.Count(m => m.Severity == Severity.Warning);

        /// <summary>Records an error.</summary>
        /// <param name="code">Stable machine-readable code.</param>
        /// <param name="message">User-facing message.</param>
        /// <param name="context">Optional context.</param>
        /// <returns>This report, so calls can be chained.</returns>
        public ValidationReport AddError(string code, string message, string? context = null) =>
            Add(new ValidationMessage(Severity.Error, code, message, context));

        /// <summary>Records a warning.</summary>
        /// <param name="code">Stable machine-readable code.</param>
        /// <param name="message">User-facing message.</param>
        /// <param name="context">Optional context.</param>
        /// <returns>This report, so calls can be chained.</returns>
        public ValidationReport AddWarning(string code, string message, string? context = null) =>
            Add(new ValidationMessage(Severity.Warning, code, message, context));

        /// <summary>Records an informational message.</summary>
        /// <param name="code">Stable machine-readable code.</param>
        /// <param name="message">User-facing message.</param>
        /// <param name="context">Optional context.</param>
        /// <returns>This report, so calls can be chained.</returns>
        public ValidationReport AddInfo(string code, string message, string? context = null) =>
            Add(new ValidationMessage(Severity.Info, code, message, context));

        /// <summary>Records an existing message.</summary>
        /// <param name="message">The message to record.</param>
        /// <returns>This report, so calls can be chained.</returns>
        public ValidationReport Add(ValidationMessage message)
        {
            _messages.Add(message ?? throw new ArgumentNullException(nameof(message)));
            return this;
        }

        /// <summary>Copies every message from another report into this one.</summary>
        /// <param name="other">The report to merge.</param>
        /// <returns>This report, so calls can be chained.</returns>
        public ValidationReport Merge(ValidationReport other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _messages.AddRange(other._messages);
            return this;
        }

        /// <summary>Renders the report as human-readable lines.</summary>
        /// <param name="maximumLines">The maximum number of messages to include.</param>
        /// <returns>A multi-line summary suitable for showing to a user.</returns>
        public string ToDisplayString(int maximumLines = 25)
        {
            if (_messages.Count == 0)
            {
                return "No issues found.";
            }

            IEnumerable<string> lines = _messages
                .OrderByDescending(m => m.Severity)
                .Take(maximumLines)
                .Select(m => m.ToString());

            string text = string.Join(Environment.NewLine, lines);
            int hidden = _messages.Count - Math.Min(_messages.Count, maximumLines);
            return hidden > 0
                ? text + Environment.NewLine + $"... and {hidden} more (see the log file for the full list)."
                : text;
        }
    }
}
