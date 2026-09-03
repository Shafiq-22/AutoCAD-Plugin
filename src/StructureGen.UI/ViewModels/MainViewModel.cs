using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Services;

namespace StructureGen.UI.ViewModels
{
    /// <summary>The palette's root view model, tying the four tabs together.</summary>
    /// <remarks>
    /// The workflow the tabs follow is the one a detailer already works in: choose a profile, set
    /// its length and quantity, add it to a list, review the list, generate, then schedule and
    /// export. Nothing forces that order, but following it left to right does the whole job.
    /// </remarks>
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly StructureGenContext _context;
        private readonly ICadService _cad;

        private string _status = "Ready.";
        private int _selectedTabIndex;

        /// <summary>Initialises a new instance of the <see cref="MainViewModel"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="cad">The CAD host.</param>
        public MainViewModel(StructureGenContext context, ICadService cad)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cad = cad ?? throw new ArgumentNullException(nameof(cad));

            Editor = new ElementEditorViewModel(context, cad, SetStatus);
            List = new ElementListViewModel(context, cad, SetStatus);
            Bom = new BomViewModel(context, cad, () => List.Definitions(), SetStatus);
            Settings = new SettingsViewModel(context, SetStatus, OnDatabaseChanged);

            AddToListCommand = new RelayCommand(AddToList);
            UpdateSelectedCommand = new RelayCommand(UpdateSelected, () => List.Selected is not null);
            UpdateInDrawingCommand = new RelayCommand(UpdateInDrawing, () => Editor.IsEditingExisting);
            GenerateAllCommand = new RelayCommand(() => Generate(false), () => List.ItemCount > 0);
            GenerateSelectedCommand = new RelayCommand(() => Generate(true), () => List.Selected is not null);
            ValidateListCommand = new RelayCommand(ValidateList, () => List.ItemCount > 0);

            ReportStartupState();
        }

        /// <summary>Gets the build-an-element tab.</summary>
        public ElementEditorViewModel Editor { get; }

        /// <summary>Gets the working list tab.</summary>
        public ElementListViewModel List { get; }

        /// <summary>Gets the bill of materials tab.</summary>
        public BomViewModel Bom { get; }

        /// <summary>Gets the settings tab.</summary>
        public SettingsViewModel Settings { get; }

        /// <summary>Gets the command that adds what the editor holds to the working list.</summary>
        public RelayCommand AddToListCommand { get; }

        /// <summary>Gets the command that replaces the selected list item with what the editor holds.</summary>
        public RelayCommand UpdateSelectedCommand { get; }

        /// <summary>Gets the command that pushes the editor's changes back onto the drawing object being amended.</summary>
        public RelayCommand UpdateInDrawingCommand { get; }

        /// <summary>Gets the command that generates every item in the list.</summary>
        public RelayCommand GenerateAllCommand { get; }

        /// <summary>Gets the command that generates only the selected item.</summary>
        public RelayCommand GenerateSelectedCommand { get; }

        /// <summary>Gets the command that checks the list without generating anything.</summary>
        public RelayCommand ValidateListCommand { get; }

        /// <summary>Gets the plugin version, shown in the palette's footer.</summary>
        public string Version => Core.Settings.PluginPaths.PluginVersion;

        /// <summary>Gets the shared status line.</summary>
        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        /// <summary>Gets or sets which tab is showing.</summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => Set(ref _selectedTabIndex, value);
        }

        /// <summary>Loads an element from the drawing into the editor.</summary>
        /// <param name="definition">The element to amend.</param>
        /// <param name="handle">The handle of the object it came from.</param>
        public void LoadForEditing(ElementDefinition definition, string handle)
        {
            Editor.LoadForEditing(definition, handle);
            SelectedTabIndex = 0;
            RelayCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Shows the settings tab.</summary>
        public void ShowSettings() => SelectedTabIndex = 3;

        /// <summary>Refreshes everything that depends on the loaded database.</summary>
        public void RefreshDatabase() => OnDatabaseChanged();

        private void AddToList()
        {
            Result<ElementDefinition> built = Editor.BuildDefinition(preserveIdentity: false);

            if (built.IsFailure)
            {
                SetStatus(built.ErrorMessage!);
                return;
            }

            List.Add(built.Value);
            SelectedTabIndex = 1;
        }

        private void UpdateSelected()
        {
            Result<ElementDefinition> built = Editor.BuildDefinition(preserveIdentity: false);

            if (built.IsFailure)
            {
                SetStatus(built.ErrorMessage!);
                return;
            }

            List.ReplaceSelected(built.Value);
        }

        private void UpdateInDrawing()
        {
            if (Editor.EditingHandle is not { } handle)
            {
                return;
            }

            // The identity is preserved so the object keeps its history through the edit; losing it
            // would sever the element from anything that already referred to it.
            Result<ElementDefinition> built = Editor.BuildDefinition(preserveIdentity: true);

            if (built.IsFailure)
            {
                SetStatus(built.ErrorMessage!);
                return;
            }

            Result<string> updated = _cad.UpdateElement(handle, built.Value);

            if (updated.IsFailure)
            {
                SetStatus(updated.ErrorMessage!);
                return;
            }

            SetStatus($"Updated '{built.Value.Designation}' in the drawing ({updated.Value}).");
            Editor.StopEditing();
            RelayCommand.RaiseCanExecuteChanged();
        }

        private void Generate(bool onlySelected)
        {
            Result<GenerationSummary> generated = List.Generate(onlySelected);

            if (generated.IsFailure)
            {
                SetStatus(generated.ErrorMessage!);
                _cad.ShowMessage("StructureGen", generated.ErrorMessage!);
                return;
            }

            GenerationSummary summary = generated.Value;
            SetStatus(summary.ToDisplayString());
            _cad.WriteMessage(summary.ToDisplayString());

            foreach (ValidationMessage message in summary.Report.Messages)
            {
                _cad.WriteMessage("  " + message);
            }

            if (summary.ObjectCount == 0)
            {
                // A run that draws nothing is a failure however the plumbing classifies it. Every
                // reason for one - no generator for the category, an outline the geometry engine
                // refused - is reported per item and leaves the run itself "successful" with an
                // empty result, so up to here it has been written only to the command line. That
                // is not good enough: a drafter working with the command line collapsed, which is
                // common, sees a palette that looks like it did nothing at all and has no thread
                // to pull. So it is said again, in a dialog, with the reasons.
                _cad.ShowMessage("StructureGen", NothingWasCreated(summary));
            }
        }

        /// <summary>Explains a generation run that produced no geometry.</summary>
        /// <param name="summary">What the run reported.</param>
        /// <returns>Text naming the reasons, or saying plainly that none were given.</returns>
        private static string NothingWasCreated(GenerationSummary summary)
        {
            System.Text.StringBuilder text = new();
            text.AppendLine("Nothing was drawn. No object was added to the drawing.");
            text.AppendLine();

            IReadOnlyList<ValidationMessage> reasons = summary.Report.Messages
                .Where(message => message.Severity != Severity.Info)
                .ToList();

            if (reasons.Count > 0)
            {
                text.AppendLine(reasons.Count == 1 ? "The reason given was:" : "The reasons given were:");

                foreach (ValidationMessage message in reasons)
                {
                    text.AppendLine($"  - {message.Message}");
                }
            }
            else
            {
                text.AppendLine(
                    "No reason was reported, which should not happen. Please raise an issue with the " +
                    "log file attached.");
            }

            text.AppendLine();
            text.Append("Full detail is in the log file: " + Core.Diagnostics.FileLogger.DefaultDirectory());

            return text.ToString();
        }

        private void ValidateList()
        {
            ValidationReport report = List.Validate();

            SetStatus(report.IsValid
                ? $"All {List.ItemCount} item(s) are ready to generate."
                : $"{report.ErrorCount} problem(s) found. {report.ToDisplayString(5)}");
        }

        private void OnDatabaseChanged()
        {
            Editor.RefreshCategories();
            SetStatus($"Database reloaded: {_context.Database.Stamp}");
        }

        private void ReportStartupState()
        {
            if (!_context.Database.IsLoaded)
            {
                Status =
                    "No structural database is loaded. Open the Settings tab, check the database path and choose " +
                    "Reload Database.";
                SelectedTabIndex = 3;
                return;
            }

            int unverified = _context.Database.Current.UnverifiedRecordCount;

            Status = unverified > 0
                ? $"Loaded {_context.Database.Stamp}. NOTE: {unverified} record(s) are not marked as verified - " +
                  "check them against your authoritative source before relying on the output."
                : $"Loaded {_context.Database.Stamp}.";
        }

        private void SetStatus(string message)
        {
            Status = message;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }
}
