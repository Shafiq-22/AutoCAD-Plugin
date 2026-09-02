using System;

namespace StructureGen.Core.Diagnostics
{
    /// <summary>
    /// Minimal logging abstraction.
    /// </summary>
    /// <remarks>
    /// StructureGen deliberately does not take a dependency on <c>Microsoft.Extensions.Logging</c>.
    /// The plugin is loaded into the AutoCAD process, which already loads its own copies of several
    /// Microsoft.Extensions assemblies; shipping another version alongside is a well known source of
    /// assembly-resolution conflicts between add-ins. The abstraction needed here is three methods
    /// wide, so it is defined locally and the plugin ships with no third-party runtime dependency.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>Writes an entry to the log.</summary>
        /// <param name="level">The severity of the entry.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">An optional exception whose detail should be recorded.</param>
        void Log(LogLevel level, string message, Exception? exception = null);
    }

    /// <summary>Convenience helpers over <see cref="ILogger"/>.</summary>
    public static class LoggerExtensions
    {
        /// <summary>Writes a debug entry.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message.</param>
        public static void Debug(this ILogger logger, string message) => logger.Log(LogLevel.Debug, message);

        /// <summary>Writes an informational entry.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message.</param>
        public static void Information(this ILogger logger, string message) => logger.Log(LogLevel.Information, message);

        /// <summary>Writes a warning entry.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message.</param>
        public static void Warning(this ILogger logger, string message) => logger.Log(LogLevel.Warning, message);

        /// <summary>Writes an error entry.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception that caused the failure, if any.</param>
        public static void Error(this ILogger logger, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Error, message, exception);
    }
}
