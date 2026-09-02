using System;
using System.Collections.Generic;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Core.Units;
using StructureGen.Data;
using StructureGen.Services;

namespace StructureGen.UI.ViewModels
{
    /// <summary>The settings tab, including the database reload the whole update story rests on.</summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly StructureGenContext _context;
        private readonly Action<string> _status;
        private readonly Action _databaseChanged;

        private string _databasePath;
        private string _validationReport = string.Empty;

        /// <summary>Initialises a new instance of the <see cref="SettingsViewModel"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="status">Reports progress and problems to the shared status line.</param>
        /// <param name="databaseChanged">Called after a successful reload so the rest of the palette refreshes.</param>
        public SettingsViewModel(StructureGenContext context, Action<string> status, Action databaseChanged)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _databaseChanged = databaseChanged ?? throw new ArgumentNullException(nameof(databaseChanged));

            _databasePath = context.Settings.DatabasePath;

            ReloadDatabaseCommand = new RelayCommand(ReloadDatabase);
            SaveCommand = new RelayCommand(Save);

            RefreshDatabaseSummary();
        }

        /// <summary>Gets the length units the user may choose between.</summary>
        public IReadOnlyList<LengthUnit> LengthUnits { get; } = new[]
        {
            LengthUnit.Millimetre, LengthUnit.Centimetre, LengthUnit.Metre, LengthUnit.Inch, LengthUnit.Foot
        };

        /// <summary>Gets the mass units the user may choose between.</summary>
        public IReadOnlyList<MassUnit> MassUnits { get; } = new[]
        {
            MassUnit.Kilogram, MassUnit.Tonne, MassUnit.Gram, MassUnit.Pound
        };

        /// <summary>Gets the detail levels the user may choose between.</summary>
        public IReadOnlyList<DetailLevel> DetailLevels { get; } = new[]
        {
            DetailLevel.Simplified, DetailLevel.Standard, DetailLevel.Detailed
        };

        /// <summary>Gets the command that reloads the structural database from disk.</summary>
        public RelayCommand ReloadDatabaseCommand { get; }

        /// <summary>Gets the command that saves the settings.</summary>
        public RelayCommand SaveCommand { get; }

        /// <summary>Gets or sets the configured database path. Relative paths resolve against the plugin's data folder.</summary>
        public string DatabasePath
        {
            get => _databasePath;
            set => Set(ref _databasePath, value);
        }

        /// <summary>Gets or sets the unit lengths are entered and displayed in.</summary>
        public LengthUnit DisplayLengthUnit
        {
            get => _context.Settings.DisplayLengthUnit;
            set
            {
                _context.Settings.DisplayLengthUnit = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the unit member lengths are reported in on the schedule.</summary>
        public LengthUnit BomLengthUnit
        {
            get => _context.Settings.BomLengthUnit;
            set
            {
                _context.Settings.BomLengthUnit = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the unit masses are displayed in.</summary>
        public MassUnit DisplayMassUnit
        {
            get => _context.Settings.DisplayMassUnit;
            set
            {
                _context.Settings.DisplayMassUnit = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the default level of geometric detail.</summary>
        public DetailLevel DefaultDetailLevel
        {
            get => _context.Settings.DefaultDetailLevel;
            set
            {
                _context.Settings.DefaultDetailLevel = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the prefix applied to layers StructureGen creates.</summary>
        public string LayerPrefix
        {
            get => _context.Settings.LayerPrefix;
            set
            {
                _context.Settings.LayerPrefix = value;
                Raise();
            }
        }

        /// <summary>Gets or sets a value indicating whether identical members share one schedule line.</summary>
        public bool GroupIdenticalElements
        {
            get => _context.Settings.GroupIdenticalElements;
            set
            {
                _context.Settings.GroupIdenticalElements = value;
                Raise();
            }
        }

        /// <summary>Gets or sets a value indicating whether the layer distinguishes two schedule lines.</summary>
        public bool GroupByLayer
        {
            get => _context.Settings.GroupByLayer;
            set
            {
                _context.Settings.GroupByLayer = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the tolerance within which two lengths count as the same, in millimetres.</summary>
        public decimal LengthGroupingToleranceMm
        {
            get => _context.Settings.LengthGroupingToleranceMm;
            set
            {
                _context.Settings.LengthGroupingToleranceMm = value;
                Raise();
            }
        }

        /// <summary>Gets or sets the text height used for generated schedule tables.</summary>
        public double BomTextHeight
        {
            get => _context.Settings.BomTextHeight;
            set
            {
                _context.Settings.BomTextHeight = value;
                Raise();
            }
        }

        /// <summary>Gets or sets a value indicating whether verbose diagnostics are written to the log.</summary>
        public bool VerboseLogging
        {
            get => _context.Settings.VerboseLogging;
            set
            {
                _context.Settings.VerboseLogging = value;
                Raise();
            }
        }

        /// <summary>Gets a description of the currently loaded database.</summary>
        public string DatabaseSummary { get; private set; } = string.Empty;

        /// <summary>Gets the absolute path the database was last loaded from.</summary>
        public string LoadedPath => _context.Database.LoadedPath ?? "(nothing loaded)";

        /// <summary>Gets the folder relative database paths resolve against.</summary>
        public string DataFolder => PluginPaths.DataDirectory;

        /// <summary>Gets the folder log files are written to.</summary>
        public string LogFolder => Core.Diagnostics.FileLogger.DefaultDirectory();

        /// <summary>Gets the file settings are persisted to.</summary>
        public string SettingsFile => _context.SettingsStore.SettingsFilePath;

        /// <summary>Gets the findings from the last database load.</summary>
        public string ValidationReport
        {
            get => _validationReport;
            private set => Set(ref _validationReport, value);
        }

        /// <summary>Reloads the structural database from disk.</summary>
        public void ReloadDatabase()
        {
            _context.Settings.DatabasePath = DatabasePath;

            Result<StructuralDatabase> result = _context.Database.Load(DatabasePath);

            if (result.IsFailure)
            {
                ValidationReport = result.ErrorMessage ?? "The database could not be loaded.";
                _status("The database was not reloaded; the previously loaded data is still in use.");
                RefreshDatabaseSummary();
                return;
            }

            ValidationReport = result.Report is { } report && report.Messages.Count > 0
                ? report.ToDisplayString(40)
                : "No issues found.";

            RefreshDatabaseSummary();
            _context.ApplySettings(_context.Settings);
            _databaseChanged();

            _status($"Reloaded {result.Value.Metadata.Stamp}: {result.Value.Sections.Count} section(s).");
        }

        /// <summary>Saves the settings.</summary>
        public void Save()
        {
            _context.Settings.DatabasePath = DatabasePath;
            Result saved = _context.ApplySettings(_context.Settings);

            _status(saved.IsSuccess
                ? $"Settings saved to {_context.SettingsStore.SettingsFilePath}"
                : saved.ErrorMessage!);
        }

        private void RefreshDatabaseSummary()
        {
            StructuralDatabase database = _context.Database.Current;

            DatabaseSummary = database.Sections.Count == 0
                ? "No structural database is loaded. Set a path above and choose Reload."
                : $"{database.Metadata.Stamp}  -  {database.Standards.Count} standard(s), " +
                  $"{database.Materials.Count} material(s), {database.Sections.Count} section(s)" +
                  (database.UnverifiedRecordCount > 0
                      ? $"  -  {database.UnverifiedRecordCount} NOT VERIFIED"
                      : "  -  all records verified");

            Raise(nameof(DatabaseSummary));
            Raise(nameof(LoadedPath));
        }
    }
}
