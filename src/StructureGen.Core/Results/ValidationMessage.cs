using System;

namespace StructureGen.Core.Results
{
    /// <summary>A single diagnostic produced while validating user input or database content.</summary>
    public sealed class ValidationMessage
    {
        /// <summary>Initialises a new instance of the <see cref="ValidationMessage"/> class.</summary>
        /// <param name="severity">How serious the finding is.</param>
        /// <param name="code">A stable machine-readable code, for example <c>DB.MissingField</c>.</param>
        /// <param name="message">A message written for the end user, free of stack traces and jargon.</param>
        /// <param name="context">Optional context such as a record id, file name or field name.</param>
        public ValidationMessage(Severity severity, string code, string message, string? context = null)
        {
            Severity = severity;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Context = context;
        }

        /// <summary>Gets how serious the finding is.</summary>
        public Severity Severity { get; }

        /// <summary>Gets the stable machine-readable code.</summary>
        public string Code { get; }

        /// <summary>Gets the user-facing message.</summary>
        public string Message { get; }

        /// <summary>Gets optional context such as a record id or field name.</summary>
        public string? Context { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Context is null
                ? $"{Severity}: [{Code}] {Message}"
                : $"{Severity}: [{Code}] {Message} ({Context})";
    }
}
