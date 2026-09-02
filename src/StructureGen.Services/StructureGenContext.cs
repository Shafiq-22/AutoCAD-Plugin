using System;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Data;
using StructureGen.Data.Json;
using StructureGen.Excel;
using StructureGen.Geometry;

namespace StructureGen.Services
{
    /// <summary>
    /// The composition root: builds the object graph once and hands it to whatever hosts it.
    /// </summary>
    /// <remarks>
    /// Dependencies are injected through constructors, but no container is used. A container would
    /// mean shipping <c>Microsoft.Extensions.DependencyInjection</c> and its dependencies into
    /// AutoCAD's process, where they can collide with the copies AutoCAD and other add-ins already
    /// load; the graph here is a dozen objects wired in a fixed order, which is exactly what a
    /// container would have done, minus the risk and the reflection.
    ///
    /// Nothing in this class is a static mutable singleton. The context is created by the AutoCAD
    /// extension application and passed to the palette, so the dependency direction stays one-way
    /// and the same graph can be built in a test with a stand-in CAD service.
    /// </remarks>
    public sealed class StructureGenContext : IDisposable
    {
        private bool _disposed;

        private StructureGenContext(
            ILogger logger,
            ISettingsStore settingsStore,
            ProjectSettings settings,
            DatabaseService database,
            ElementGeneratorRegistry generators,
            ElementFactory elements,
            MassCalculator mass,
            BomService bom)
        {
            Logger = logger;
            SettingsStore = settingsStore;
            Settings = settings;
            Database = database;
            Generators = generators;
            Elements = elements;
            Mass = mass;
            Bom = bom;
        }

        /// <summary>Gets the logger every component writes technical detail to.</summary>
        public ILogger Logger { get; }

        /// <summary>Gets the settings store.</summary>
        public ISettingsStore SettingsStore { get; }

        /// <summary>Gets the current settings.</summary>
        public ProjectSettings Settings { get; private set; }

        /// <summary>Gets the structural database service.</summary>
        public DatabaseService Database { get; }

        /// <summary>Gets the element generator registry.</summary>
        public ElementGeneratorRegistry Generators { get; }

        /// <summary>Gets the element factory.</summary>
        public ElementFactory Elements { get; }

        /// <summary>Gets the mass calculator.</summary>
        public MassCalculator Mass { get; }

        /// <summary>Gets the bill of materials service.</summary>
        public BomService Bom { get; }

        /// <summary>
        /// Gets the report produced by the initial database load, so the host can show it once at
        /// start-up rather than interrupting the user later.
        /// </summary>
        public Result<StructuralDatabase>? StartupLoadResult { get; private set; }

        /// <summary>Builds the whole object graph and performs the initial database load.</summary>
        /// <param name="logger">
        /// The logger to use. When <see langword="null"/> a rolling file logger under the user's
        /// local application data folder is created.
        /// </param>
        /// <param name="settingsStore">
        /// The settings store to use. When <see langword="null"/> a JSON store under the user's
        /// application data folder is created.
        /// </param>
        /// <returns>The context.</returns>
        public static StructureGenContext Create(ILogger? logger = null, ISettingsStore? settingsStore = null)
        {
            ILogger resolvedLogger = logger ?? new FileLogger();
            ISettingsStore resolvedStore = settingsStore ?? new JsonSettingsStore(resolvedLogger);
            ProjectSettings settings = resolvedStore.Load();

            if (settings.VerboseLogging)
            {
                resolvedLogger.Information("Verbose logging is enabled.");
            }

            // The registry needs a way to look records up, and the database service needs the
            // registry's knowledge of required dimensions in order to validate. The cycle is broken
            // with a closure that resolves against whatever database is loaded at the time.
            DatabaseService? databaseHolder = null;
            ElementGeneratorRegistry generators = ElementGeneratorRegistry.CreateDefault(
                recordId => databaseHolder?.FindRecord(recordId));

            DatabaseValidator validator = new(generators);
            JsonDatabaseLoader loader = new(resolvedLogger, validator);
            DatabaseService database = new(loader, resolvedLogger);
            databaseHolder = database;

            ElementFactory elements = new(database, generators);
            MassCalculator mass = new(generators);
            BomWorkbookBuilder workbooks = new(resolvedLogger);
            BomService bom = new(mass, database, workbooks, resolvedLogger);

            StructureGenContext context = new(
                resolvedLogger, resolvedStore, settings, database, generators, elements, mass, bom);

            context.StartupLoadResult = database.Load(settings.DatabasePath);

            resolvedLogger.Information(
                $"StructureGen {PluginPaths.PluginVersion} started. Data folder: '{PluginPaths.DataDirectory}'. " +
                $"Database: {database.Stamp}.");

            return context;
        }

        /// <summary>Replaces the current settings and persists them.</summary>
        /// <param name="settings">The settings to adopt.</param>
        /// <returns>The outcome of persisting them.</returns>
        public Result ApplySettings(ProjectSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            return SettingsStore.Save(settings);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            (Logger as IDisposable)?.Dispose();
        }
    }
}
