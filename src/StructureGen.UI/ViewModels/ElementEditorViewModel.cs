using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Data;
using StructureGen.Geometry;
using StructureGen.Services;

namespace StructureGen.UI.ViewModels
{
    /// <summary>
    /// The build-an-element half of the palette: category, standard, profile, then the parameters
    /// that profile actually allows.
    /// </summary>
    /// <remarks>
    /// The cascade is deliberate. Choosing a category narrows the standards to those that publish
    /// one; choosing a standard narrows the profiles; choosing a profile fills in the dimensions and
    /// decides which of them the user may touch. A drafter is never shown a field that does not
    /// apply to what they have selected, which is the difference between a usable palette and a form
    /// with forty greyed-out boxes.
    /// </remarks>
    public sealed class ElementEditorViewModel : ViewModelBase
    {
        private readonly StructureGenContext _context;
        private readonly ICadService _cad;
        private readonly Action<string> _status;

        private CategoryDescriptor? _category;
        private StandardRecord? _standard;
        private SectionRecord? _profile;
        private decimal _lengthMm = 6000m;
        private int _quantity = 1;
        private DetailLevel _detailLevel = DetailLevel.Standard;
        private RepresentationMode _representation = RepresentationMode.Solid3D;
        private string? _layer;
        private string? _notes;
        private bool _customMode;
        private Placement? _placement;
        private string _placementDescription = "Not placed - will be inserted at the drawing origin.";
        private Guid? _editingElementId;
        private string? _editingHandle;

        /// <summary>Initialises a new instance of the <see cref="ElementEditorViewModel"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="cad">The CAD host.</param>
        /// <param name="status">Reports progress and problems to the shared status line.</param>
        public ElementEditorViewModel(StructureGenContext context, ICadService cad, Action<string> status)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cad = cad ?? throw new ArgumentNullException(nameof(cad));
            _status = status ?? throw new ArgumentNullException(nameof(status));

            PickPointCommand = new RelayCommand(PickPoint, () => _cad.HasActiveDocument);
            PickSegmentCommand = new RelayCommand(PickSegment, () => _cad.HasActiveDocument && LengthApplies);
            ClearPlacementCommand = new RelayCommand(ClearPlacement);
            ResetParametersCommand = new RelayCommand(ResetParameters, () => Profile is not null);

            RefreshCategories();
        }

        /// <summary>Gets the categories that the loaded database actually contains records for.</summary>
        public ObservableCollection<CategoryDescriptor> Categories { get; } = new();

        /// <summary>Gets the standards that publish a profile in the chosen category.</summary>
        public ObservableCollection<StandardRecord> Standards { get; } = new();

        /// <summary>Gets the profiles available in the chosen category and standard.</summary>
        public ObservableCollection<SectionRecord> Profiles { get; } = new();

        /// <summary>Gets the parameters of the chosen profile.</summary>
        public ObservableCollection<ParameterRowViewModel> Parameters { get; } = new();

        /// <summary>Gets the detail levels the user may choose between.</summary>
        public IReadOnlyList<DetailLevel> DetailLevels { get; } =
            new[] { DetailLevel.Simplified, DetailLevel.Standard, DetailLevel.Detailed };

        /// <summary>Gets the representations the user may choose between.</summary>
        public IReadOnlyList<RepresentationMode> Representations { get; } =
            new[] { RepresentationMode.Solid3D, RepresentationMode.Profile2D, RepresentationMode.Both };

        /// <summary>Gets the command that asks the user to pick an insertion point.</summary>
        public RelayCommand PickPointCommand { get; }

        /// <summary>Gets the command that asks the user to pick a start and an end point.</summary>
        public RelayCommand PickSegmentCommand { get; }

        /// <summary>Gets the command that forgets any picked placement.</summary>
        public RelayCommand ClearPlacementCommand { get; }

        /// <summary>Gets the command that returns every parameter to its published value.</summary>
        public RelayCommand ResetParametersCommand { get; }

        /// <summary>Gets or sets the chosen category.</summary>
        public CategoryDescriptor? Category
        {
            get => _category;
            set
            {
                if (Set(ref _category, value))
                {
                    RefreshStandards();
                    RaiseApplicability();
                }
            }
        }

        /// <summary>Gets or sets the chosen standard.</summary>
        public StandardRecord? Standard
        {
            get => _standard;
            set
            {
                if (Set(ref _standard, value))
                {
                    RefreshProfiles();
                }
            }
        }

        /// <summary>Gets or sets the chosen profile.</summary>
        public SectionRecord? Profile
        {
            get => _profile;
            set
            {
                if (Set(ref _profile, value))
                {
                    RefreshParameters();
                    Raise(nameof(ProfileSummary));
                    Raise(nameof(SourceWarning));
                    Raise(nameof(HasSourceWarning));
                }
            }
        }

        /// <summary>Gets or sets the member length in millimetres.</summary>
        public decimal LengthMm
        {
            get => _lengthMm;
            set => Set(ref _lengthMm, value);
        }

        /// <summary>Gets or sets how many pieces the item represents.</summary>
        public int Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value);
        }

        /// <summary>Gets or sets how much geometric detail to generate.</summary>
        public DetailLevel DetailLevel
        {
            get => _detailLevel;
            set => Set(ref _detailLevel, value);
        }

        /// <summary>Gets or sets which representations to generate.</summary>
        public RepresentationMode Representation
        {
            get => _representation;
            set => Set(ref _representation, value);
        }

        /// <summary>Gets or sets the target layer, or <see langword="null"/> for the automatic one.</summary>
        public string? Layer
        {
            get => _layer;
            set => Set(ref _layer, value);
        }

        /// <summary>Gets or sets free text carried to the bill of materials.</summary>
        public string? Notes
        {
            get => _notes;
            set => Set(ref _notes, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user has deliberately unlocked the
        /// standardised dimensions.
        /// </summary>
        public bool CustomMode
        {
            get => _customMode;
            set
            {
                if (Set(ref _customMode, value))
                {
                    foreach (ParameterRowViewModel parameter in Parameters)
                    {
                        parameter.CustomMode = value;
                    }

                    Raise(nameof(CustomModeWarning));
                }
            }
        }

        /// <summary>Gets the warning shown while custom mode is on.</summary>
        public string CustomModeWarning => CustomMode
            ? "Custom mode is on. Anything you change here no longer matches the published profile, and every item " +
              "you add will be marked as non-standard on the schedule."
            : string.Empty;

        /// <summary>Gets a value indicating whether the chosen category takes a member length.</summary>
        public bool LengthApplies => Category?.LengthIsUserParameter ?? false;

        /// <summary>Gets a description of where the element will be placed.</summary>
        public string PlacementDescription
        {
            get => _placementDescription;
            private set => Set(ref _placementDescription, value);
        }

        /// <summary>Gets a value indicating whether the editor is amending an existing drawing object.</summary>
        public bool IsEditingExisting => _editingHandle is not null;

        /// <summary>Gets the handle of the object being amended, when there is one.</summary>
        public string? EditingHandle => _editingHandle;

        /// <summary>Gets a one-line summary of the chosen profile.</summary>
        public string ProfileSummary
        {
            get
            {
                if (Profile is null)
                {
                    return "Choose a category, a standard and a profile.";
                }

                string weight = Profile.UnitWeightKgPerM is { } unitWeight
                    ? $"{unitWeight.ToString("0.###", CultureInfo.InvariantCulture)} kg/m"
                    : Profile.PieceMassKg is { } pieceMass
                        ? $"{pieceMass.ToString("0.####", CultureInfo.InvariantCulture)} kg per piece"
                        : "weight derived from the material density";

                return $"{Profile.Designation}  -  {weight}" +
                       (Profile.Material is null ? string.Empty : $"  -  {Profile.Material} {Profile.Grade}".TrimEnd());
            }
        }

        /// <summary>Gets the warning shown when the chosen profile's data is not verified.</summary>
        public string SourceWarning => Profile is null || Profile.Verified
            ? string.Empty
            : "This profile's data is NOT marked as verified. " +
              (Profile.Provenance ?? "Check it against your authoritative copy of the standard before relying on it.");

        /// <summary>Gets a value indicating whether a source warning should be shown.</summary>
        public bool HasSourceWarning => SourceWarning.Length > 0;

        /// <summary>Rebuilds the category list from the loaded database.</summary>
        public void RefreshCategories()
        {
            string? previous = Category?.Id;

            Categories.Clear();

            foreach (string categoryId in _context.Database.Current.Categories)
            {
                Categories.Add(CategoryCatalog.FindOrGeneric(categoryId));
            }

            Category = Categories.FirstOrDefault(c => c.Id == previous) ?? Categories.FirstOrDefault();
        }

        /// <summary>Loads an existing element into the editor so its parameters can be amended.</summary>
        /// <param name="definition">The element to amend.</param>
        /// <param name="handle">The handle of the object it came from.</param>
        public void LoadForEditing(ElementDefinition definition, string handle)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _editingElementId = definition.ElementId;
            _editingHandle = handle;

            Category = Categories.FirstOrDefault(c => c.Id == definition.CategoryId) ?? Category;
            Standard = Standards.FirstOrDefault(s => s.Id == definition.StandardId) ?? Standard;

            SectionRecord? record = _context.Database.FindRecord(definition.SectionRecordId);
            Profile = record is not null
                ? Profiles.FirstOrDefault(p => p.Id == record.Id) ?? record
                : Profiles.FirstOrDefault(p => p.Designation == definition.Designation);

            CustomMode = definition.IsCustom;

            foreach (ParameterRowViewModel parameter in Parameters)
            {
                if (definition.Dimension(parameter.Key) is { } value)
                {
                    parameter.Value = value;
                }
            }

            LengthMm = definition.LengthMm ?? LengthMm;
            Quantity = definition.Quantity;
            DetailLevel = definition.DetailLevel;
            Representation = definition.Representation;
            Layer = definition.Layer;
            Notes = definition.Notes;

            if (definition.Placements.Count > 0)
            {
                _placement = definition.Placements[0];
                PlacementDescription = Describe(_placement);
            }

            Raise(nameof(IsEditingExisting));
            Raise(nameof(EditingHandle));
            _status($"Editing '{definition.Designation}' ({handle}).");
        }

        /// <summary>Forgets that the editor was amending an existing object.</summary>
        public void StopEditing()
        {
            _editingElementId = null;
            _editingHandle = null;
            Raise(nameof(IsEditingExisting));
            Raise(nameof(EditingHandle));
        }

        /// <summary>Builds an element from what the user has entered.</summary>
        /// <param name="preserveIdentity">
        /// Whether to keep the identity of the element being amended. Set when updating an existing
        /// object so it does not lose its history; clear when adding a new item to the list.
        /// </param>
        /// <returns>The element, or a failure describing what is wrong with the entries.</returns>
        public Result<ElementDefinition> BuildDefinition(bool preserveIdentity)
        {
            if (Profile is null)
            {
                return Result<ElementDefinition>.Failure(
                    "Ui.NoProfile", "Choose a profile before adding an item.");
            }

            if (Quantity < 1)
            {
                return Result<ElementDefinition>.Failure(
                    "Ui.BadQuantity", "The quantity must be at least one.");
            }

            if (LengthApplies && LengthMm <= 0m)
            {
                return Result<ElementDefinition>.Failure(
                    "Ui.BadLength", "Enter a length greater than zero.");
            }

            Dictionary<string, decimal> overrides = Parameters
                .Where(p => p.IsChangedFromStandard)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            IReadOnlyList<Placement> placements = _placement is null
                ? Array.Empty<Placement>()
                : new[] { _placement };

            return _context.Elements.Create(
                Profile,
                LengthApplies ? LengthMm : null,
                Quantity,
                DetailLevel,
                Representation,
                placements,
                overrides,
                string.IsNullOrWhiteSpace(Layer) ? null : Layer,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes,
                null,
                CustomMode,
                preserveIdentity ? _editingElementId : null);
        }

        private void RefreshStandards()
        {
            string? previous = Standard?.Id;

            Standards.Clear();

            if (Category is not null)
            {
                foreach (StandardRecord standard in _context.Database.Current.StandardsInCategory(Category.Id))
                {
                    Standards.Add(standard);
                }
            }

            Standard = Standards.FirstOrDefault(s => s.Id == previous) ?? Standards.FirstOrDefault();
        }

        private void RefreshProfiles()
        {
            string? previous = Profile?.Id;

            Profiles.Clear();

            if (Category is not null && Standard is not null)
            {
                foreach (SectionRecord record in _context.Database.Current.SectionsInCategory(Category.Id, Standard.Id))
                {
                    Profiles.Add(record);
                }
            }

            Profile = Profiles.FirstOrDefault(p => p.Id == previous) ?? Profiles.FirstOrDefault();
        }

        private void RefreshParameters()
        {
            Parameters.Clear();

            if (Profile is null)
            {
                return;
            }

            IElementGenerator? generator = _context.Generators.Find(Profile.CategoryId);
            if (generator is null)
            {
                _status(
                    $"'{Profile.Designation}' belongs to a category with no geometry generator. It can be scheduled " +
                    "but not drawn.");
                return;
            }

            ParameterSchema schema = generator.GetParameterSchema(Profile);

            foreach (ParameterDescriptor descriptor in schema.Parameters)
            {
                if (InstanceParameterKeys.IsInstanceKey(descriptor.Key))
                {
                    continue;
                }

                if (Profile.Dimension(descriptor.Key) is not { } value)
                {
                    // A parameter the record does not carry is omitted rather than shown empty, so
                    // an equal angle does not display a blank second leg.
                    if (descriptor.IsRequired)
                    {
                        _status($"'{Profile.Designation}' has no value for '{descriptor.DisplayName}'.");
                    }

                    continue;
                }

                Parameters.Add(new ParameterRowViewModel(descriptor, value) { CustomMode = CustomMode });
            }
        }

        private void RaiseApplicability()
        {
            Raise(nameof(LengthApplies));
            RelayCommand.RaiseCanExecuteChanged();
        }

        private void PickPoint()
        {
            Result<Vec3> picked = _cad.PickPoint("Pick the insertion point");

            if (picked.IsFailure)
            {
                _status(picked.ErrorMessage!);
                return;
            }

            _placement = new Placement(picked.Value, Vec3.UnitX);
            PlacementDescription = Describe(_placement);
            _status("Insertion point set.");
        }

        private void PickSegment()
        {
            Result<(Placement Placement, decimal LengthMm)> picked =
                _cad.PickSegment("Pick the start of the member", "Pick the end of the member");

            if (picked.IsFailure)
            {
                _status(picked.ErrorMessage!);
                return;
            }

            _placement = picked.Value.Placement;

            // The length comes from the two picks, which is the whole point of picking a segment.
            // The cross-section is untouched: only the extrusion distance changes.
            LengthMm = decimal.Round(picked.Value.LengthMm, 3);
            PlacementDescription = Describe(_placement) +
                                   $"  Measured length {LengthMm.ToString("0.###", CultureInfo.InvariantCulture)} mm.";

            _status($"Length measured from the drawing: {LengthMm.ToString("0.###", CultureInfo.InvariantCulture)} mm.");
        }

        private void ClearPlacement()
        {
            _placement = null;
            PlacementDescription = "Not placed - will be inserted at the drawing origin.";
        }

        private void ResetParameters()
        {
            foreach (ParameterRowViewModel parameter in Parameters)
            {
                parameter.ResetToStandard();
            }

            CustomMode = false;
            _status("Parameters returned to the published values.");
        }

        private static string Describe(Placement placement) =>
            $"Origin {placement.Origin}, axis {placement.AxisDirection}.";
    }
}
