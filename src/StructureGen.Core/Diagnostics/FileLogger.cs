using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StructureGen.Core.Diagnostics
{
    /// <summary>
    /// Writes log entries to a daily rolling file under the user's local application data folder.
    /// </summary>
    /// <remarks>
    /// The technical detail that must never be shown to a drafter — stack traces, AutoCAD error
    /// statuses, file paths — is written here, while the user interface shows only the friendly
    /// message. The log directory is resolved at run time from the platform, so no absolute or
    /// user-specific path is ever compiled into the assembly.
    /// </remarks>
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly object _gate = new();
        private readonly string _directory;
        private readonly int _retainedDays;
        private readonly LogLevel _minimumLevel;
        private bool _disposed;

        /// <summary>Initialises a new instance of the <see cref="FileLogger"/> class.</summary>
        /// <param name="directory">
        /// The directory to write into. When <see langword="null"/> the default
        /// <c>&lt;LocalApplicationData&gt;/StructureGen/logs</c> location is used.
        /// </param>
        /// <param name="minimumLevel">Entries below this level are discarded.</param>
        /// <param name="retainedDays">How many days of log files to keep.</param>
        public FileLogger(string? directory = null, LogLevel minimumLevel = LogLevel.Information, int retainedDays = 14)
        {
            _directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : directory!;
            _minimumLevel = minimumLevel;
            _retainedDays = Math.Max(1, retainedDays);
            Directory.CreateDirectory(_directory);
            PurgeExpiredFiles();
        }

        /// <summary>Gets the directory log files are written to.</summary>
        public string LogDirectory => _directory;

        /// <summary>Gets the full path of the file the next entry will be written to.</summary>
        public string CurrentFilePath =>
            Path.Combine(_directory, $"structuregen-{DateTime.Now:yyyyMMdd}.log");

        /// <summary>Computes the default log directory for the current user and platform.</summary>
        /// <returns>The directory path.</returns>
        public static string DefaultDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StructureGen",
            "logs");

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (_disposed || level < _minimumLevel)
            {
                return;
            }

            StringBuilder builder = new();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                   .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
                   .Append(message);

            if (exception is not null)
            {
                builder.AppendLine()
                       .Append("    ")
                       .Append(exception.GetType().FullName)
                       .Append(": ")
                       .Append(exception.Message);

                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    builder.AppendLine().Append(exception.StackTrace);
                }

                Exception? inner = exception.InnerException;
                while (inner is not null)
                {
                    builder.AppendLine().Append("  --> ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
                    inner = inner.InnerException;
                }
            }

            builder.AppendLine();

            lock (_gate)
            {
                try
                {
                    File.AppendAllText(CurrentFilePath, builder.ToString(), Encoding.UTF8);
                }
                catch (IOException)
                {
                    // Logging must never break the host application. A locked or full disk is
                    // silently tolerated; the user interface still shows the friendly message.
                }
                catch (UnauthorizedAccessException)
                {
                    // As above: the plugin keeps working when the log location is not writable.
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() => _disposed = true;

        private void PurgeExpiredFiles()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-_retainedDays);
                IEnumerable<string> files = Directory.EnumerateFiles(_directory, "structuregen-*.log");
                foreach (string file in files)
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (IOException)
            {
                // Housekeeping is best-effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Housekeeping is best-effort.
            }
        }
    }
}
