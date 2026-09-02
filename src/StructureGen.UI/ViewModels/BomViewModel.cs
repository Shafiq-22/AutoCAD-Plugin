using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using StructureGen.Bom;
using StructureGen.Core.Results;
using StructureGen.Services;

namespace StructureGen.UI.ViewModels
{
    /// <summary>The bill of materials tab.</summary>
    /// <remarks>
    /// Two sources are offered and the distinction between them is made explicit in the interface,
    /// because it matters. The schedule built from the drawing is the real one: it counts the
    /// objects that exist, so a member erased or copied by hand is reflected. The preview built from
    /// the working list is a convenience for checking a weight before committing anything, and it is
    /// labelled as not yet drawn wherever it appears.
    /// </remarks>
    public sealed class BomViewModel : ViewModelBase
    {
        private readonly StructureGenContext _context;
        private readonly ICadService _cad;
        private readonly Func<IReadOnlyList<Core.Model.ElementDefinition>> _listSource;
        private readonly Action<string> _status;

        private BomDocument? _document;
        private bool _isPreview;

        /// <summary>Initialises a new instance of the <see cref="BomViewModel"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="cad">The CAD host.</param>
        /// <param name="listSource">Supplies the working list when a preview is requested.</param>
        /// <param name="status">Reports progress and problems to the shared status line.</param>
        public BomViewModel(
            StructureGenContext context,
            ICadService cad,
            Func<IReadOnlyList<Core.Model.ElementDefinition>> listSource,
            Action<string> status)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cad = cad ?? throw new ArgumentNullException(nameof(cad));
            _listSource = listSource ?? throw new ArgumentNullException(nameof(listSource));
            _status = status ?? throw new ArgumentNullException(nameof(status));

            BuildFromDrawingCommand = new RelayCommand(BuildFromDrawing, () => _cad.HasActiveDocument);
            PreviewFromListCommand = new RelayCommand(PreviewFromList);
            InsertInModelCommand = new RelayCommand(
                () => InsertTable(BomTablePlacement.ModelSpace), () => CanPlaceInDrawing);
            InsertInLayoutCommand = new RelayCommand(
                () => InsertTable(BomTablePlacement.CurrentLayout), () => CanPlaceInDrawing);
            ExportCommand = new RelayCommand(Export, () => _document is { IsEmpty: false });
        }

        /// <summary>Gets the schedule lines currently displayed.</summary>
        public ObservableCollection<BomLine> Lines { get; } = new();

        /// <summary>Gets the command that builds the schedule from the drawing.</summary>
        public RelayCommand BuildFromDrawingCommand { get; }

        /// <summary>Gets the command that previews the schedule from the working list.</summary>
        public RelayCommand PreviewFromListCommand { get; }

        /// <summary>Gets the command that draws the schedule as a table in model space.</summary>
        public RelayCommand InsertInModelCommand { get; }

        /// <summary>Gets the command that draws the schedule as a table in the current layout.</summary>
        public RelayCommand InsertInLayoutCommand { get; }

        /// <summary>Gets the command that exports the schedule to a workbook.</summary>
        public RelayCommand ExportCommand { get; }

        /// <summary>Gets the schedule currently displayed, if any.</summary>
        public BomDocument? Document => _document;

        /// <summary>Gets a value indicating whether the displayed schedule is a not-yet-drawn preview.</summary>
        public bool IsPreview
        {
            get => _isPreview;
            private set
            {
                if (Set(ref _isPreview, value))
                {
                    Raise(nameof(SourceDescription));
                    Raise(nameof(CanPlaceInDrawing));
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets a value indicating whether the schedule may be drawn into the drawing.</summary>
        public bool CanPlaceInDrawing => _document is { IsEmpty: false } && _cad.HasActiveDocument;

        /// <summary>Gets a description of where the displayed schedule came from.</summary>
        public string SourceDescription => _document is null
            ? "No schedule has been built yet."
            : IsPreview
                ? "PREVIEW from the working list. Nothing has been drawn yet; the schedule below is what would be created."
                : _document.SourceDescription ?? "Built from the drawing.";

        /// <summary>Gets the grand totals as a single line of text.</summary>
        public string TotalsSummary
        {
            get
            {
                if (_document is null)
                {
                    return string.Empty;
                }

                BomTotals totals = _document.Totals;

                return $"{totals.LineCount} line(s)   {totals.TotalPieces} piece(s)   " +
                       $"{totals.TotalLengthIn(_document.Options.LengthUnit).ToString("N3", CultureInfo.InvariantCulture)} " +
                       $"{Core.Units.UnitConverter.Abbreviation(_document.Options.LengthUnit)}   " +
                       $"{totals.TotalMassKg.ToString("N2", CultureInfo.InvariantCulture)} kg " +
                       $"({totals.TotalMassTonnes.ToString("N3", CultureInfo.InvariantCulture)} t)";
            }
        }

        /// <summary>Gets the warnings that must be read alongside the schedule.</summary>
        public string Caveats => _document is null
            ? string.Empty
            : string.Join(Environment.NewLine, _document.Caveats());

        /// <summary>Gets a value indicating whether there are caveats to show.</summary>
        public bool HasCaveats => Caveats.Length > 0;

        /// <summary>Rebuilds the schedule from the drawing.</summary>
        public void BuildFromDrawing()
        {
            Result<IReadOnlyList<BomEntry>> harvested = _cad.HarvestElements(HarvestScope.EntireDrawing);

            if (harvested.IsFailure)
            {
                _status(harvested.ErrorMessage!);
                return;
            }

            if (harvested.Value.Count == 0)
            {
                _status("No StructureGen elements were found in the drawing.");
                Show(null, false);
                return;
            }

            BomOptions options = BomService.OptionsFrom(_context.Settings);
            options.DrawingReference = Path.GetFileNameWithoutExtension(_cad.DocumentName ?? string.Empty);

            BomDocument document = _context.Bom.Build(
                harvested.Value, options, $"The drawing ({Path.GetFileName(_cad.DocumentName ?? "unsaved")})");

            _context.Bom.LogSummary(document);
            Show(document, false);
            _status($"Schedule built from the drawing: {document.Lines.Count} line(s).");
        }

        private void PreviewFromList()
        {
            IReadOnlyList<Core.Model.ElementDefinition> definitions = _listSource();

            if (definitions.Count == 0)
            {
                _status("The working list is empty, so there is nothing to preview.");
                return;
            }

            BomDocument document = _context.Bom.BuildPreview(definitions, BomService.OptionsFrom(_context.Settings));
            Show(document, true);
            _status($"Preview built from the working list: {document.Lines.Count} line(s).");
        }

        private void InsertTable(BomTablePlacement placement)
        {
            if (_document is null)
            {
                return;
            }

            if (IsPreview)
            {
                _status(
                    "This schedule is a preview of items that have not been drawn. Generate them first, then build " +
                    "the schedule from the drawing.");
                return;
            }

            Result<string> inserted = _cad.InsertBomTable(_document, placement);
            _status(inserted.IsSuccess ? $"Schedule table created ({inserted.Value})." : inserted.ErrorMessage!);
        }

        private void Export()
        {
            if (_document is null)
            {
                return;
            }

            string path = _context.Bom.SuggestExportPath(_cad.DocumentName, _context.Settings.LastExportDirectory);
            Result<string> exported = _context.Bom.Export(_document, path);

            if (exported.IsFailure)
            {
                _status(exported.ErrorMessage!);
                return;
            }

            _context.Settings.LastExportDirectory = Path.GetDirectoryName(exported.Value);
            _context.ApplySettings(_context.Settings);
            _status($"Exported to {exported.Value}");
        }

        private void Show(BomDocument? document, bool isPreview)
        {
            _document = document;

            Lines.Clear();

            if (document is not null)
            {
                foreach (BomLine line in document.Lines)
                {
                    Lines.Add(line);
                }
            }

            IsPreview = isPreview;

            Raise(nameof(Document));
            Raise(nameof(TotalsSummary));
            Raise(nameof(Caveats));
            Raise(nameof(HasCaveats));
            Raise(nameof(SourceDescription));
            Raise(nameof(CanPlaceInDrawing));
            RelayCommand.RaiseCanExecuteChanged();
        }
    }
}
