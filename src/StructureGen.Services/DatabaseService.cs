using System;
using System.IO;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Data;

namespace StructureGen.Services
{
    /// <summary>
    /// Holds the currently loaded structural database and reloads it on demand.
    /// </summary>
    /// <remarks>
    /// Replacing the data file and pressing Reload Database is the whole update mechanism: nothing
    /// is compiled in, so a new revision of a user's section tables never needs a new build of the
    /// plugin. A failed reload leaves the previously loaded data in place, so a typo in an edited
    /// file cannot leave the palette with nothing to work from mid-session.
    /// </remarks>
    public sealed class DatabaseService
    {
        private readonly IDatabaseLoader _loader;
        private readonly ILogger _logger;
        private readonly object _gate = new();

        private StructuralDatabase _database = StructuralDatabase.Empty;

        /// <summary>Initialises a new instance of the <see cref="DatabaseService"/> class.</summary>
        /// <param name="loader">Reads a database from a file.</param>
        /// <param name="logger">Receives technical detail about load failures.</param>
        public DatabaseService(IDatabaseLoader loader, ILogger logger)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Gets the currently loaded database. Never <see langword="null"/>.</summary>
        public StructuralDatabase Current
        {
            get
            {
                lock (_gate)
                {
                    return _database;
                }
            }
        }

        /// <summary>Gets the absolute path the database was last loaded from, if any.</summary>
        public string? LoadedPath { get; private set; }

        /// <summary>Gets the report produced by the last load attempt.</summary>
        public ValidationReport? LastReport { get; private set; }

        /// <summary>Gets a value indicating whether a database has been loaded successfully.</summary>
        public bool IsLoaded => Current.Sections.Count > 0;

        /// <summary>Raised after a successful load, so the user interface can refresh.</summary>
        public event EventHandler? DatabaseChanged;

        /// <summary>Resolves a section record by identifier from the currently loaded database.</summary>
        /// <param name="recordId">The identifier to look up.</param>
        /// <returns>The record, or <see langword="null"/> when it is not present.</returns>
        public SectionRecord? FindRecord(string recordId) => Current.FindSection(recordId);

        /// <summary>Loads a database from a configured, possibly relative, path.</summary>
        /// <param name="configuredPath">The path as held in the settings.</param>
        /// <returns>The loaded database, or a failure carrying a user-facing reason.</returns>
        public Result<StructuralDatabase> Load(string configuredPath)
        {
            string absolute;
            try
            {
                absolute = PluginPaths.ResolveDataPath(configuredPath);
            }
            catch (ArgumentException exception)
            {
                _logger.Error($"The configured database path '{configuredPath}' is not usable.", exception);
                return Result<StructuralDatabase>.Failure(
                    "Db.BadPath",
                    "No structural database file has been configured. Set one in the StructureGen settings.");
            }

            Result<StructuralDatabase> result = _loader.Load(absolute);
            LastReport = result.Report;

            if (result.IsFailure)
            {
                _logger.Warning($"Keeping the previously loaded database because '{absolute}' could not be loaded.");
                return result;
            }

            lock (_gate)
            {
                _database = result.Value;
            }

            LoadedPath = absolute;
            DatabaseChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        /// <summary>Reloads from the path last used.</summary>
        /// <returns>The reloaded database, or a failure carrying a user-facing reason.</returns>
        public Result<StructuralDatabase> Reload()
        {
            if (LoadedPath is null)
            {
                return Result<StructuralDatabase>.Failure(
                    "Db.NeverLoaded", "No structural database has been loaded yet, so there is nothing to reload.");
            }

            return Load(LoadedPath);
        }

        /// <summary>Describes the loaded database for stamping onto drawings and schedules.</summary>
        /// <returns>A short label such as <c>EN sections v2.1</c>.</returns>
        public string Stamp => IsLoaded
            ? $"{Current.Metadata.Stamp} ({Path.GetFileName(LoadedPath)})"
            : "no database loaded";
    }
}
