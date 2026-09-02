using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Services;

namespace StructureGen.UI.ViewModels
{
    /// <summary>
    /// The working list: several different items assembled and reviewed before any of them is
    /// committed to the drawing.
    /// </summary>
    /// <remarks>
    /// Nothing here has touched the drawing yet. That separation is what lets a detailer put
    /// together a whole frame's worth of members, check the running weight, fix the one that is
    /// wrong, and only then generate. Once generated, the drawing becomes the authority and the
    /// schedule is harvested from it rather than from this list.
    /// </remarks>
    public sealed class ElementListViewModel : ViewModelBase
    {
        private readonly StructureGenContext _context;
        private readonly ICadService _cad;
        private readonly Action<string> _status;

        private ElementListItemViewModel? _selected;

        /// <summary>Initialises a new instance of the <see cref="ElementListViewModel"/> class.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="cad">The CAD host.</param>
        /// <param name="status">Reports progress and problems to the shared status line.</param>
        public ElementListViewModel(StructureGenContext context, ICadService cad, Action<string> status)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cad = cad ?? throw new ArgumentNullException(nameof(cad));
            _status = status ?? throw new ArgumentNullException(nameof(status));

            DuplicateCommand = new RelayCommand(Duplicate, () => Selected is not null);
            DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
            MoveUpCommand = new RelayCommand(MoveUp, () => Selected is not null && Items.IndexOf(Selected) > 0);
            MoveDownCommand = new RelayCommand(
                MoveDown, () => Selected is not null && Items.IndexOf(Selected) < Items.Count - 1);
            ClearCommand = new RelayCommand(Clear, () => Items.Count > 0);

            Items.CollectionChanged += (_, _) =>
            {
                Renumber();
                RaiseTotals();
            };
        }

        /// <summary>Gets the items waiting to be generated.</summary>
        public ObservableCollection<ElementListItemViewModel> Items { get; } = new();

        /// <summary>Gets or sets the item the user has selected.</summary>
        public ElementListItemViewModel? Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the command that copies the selected item.</summary>
        public RelayCommand DuplicateCommand { get; }

        /// <summary>Gets the command that removes the selected item.</summary>
        public RelayCommand DeleteCommand { get; }

        /// <summary>Gets the command that moves the selected item earlier in the list.</summary>
        public RelayCommand MoveUpCommand { get; }

        /// <summary>Gets the command that moves the selected item later in the list.</summary>
        public RelayCommand MoveDownCommand { get; }

        /// <summary>Gets the command that empties the list.</summary>
        public RelayCommand ClearCommand { get; }

        /// <summary>Gets the number of items in the list.</summary>
        public int ItemCount => Items.Count;

        /// <summary>Gets the total number of pieces the list represents.</summary>
        public int TotalPieces => Items.Sum(i => i.Quantity);

        /// <summary>Gets the running total weight of the list.</summary>
        public string TotalWeight
        {
            get
            {
                decimal total = 0m;
                int unknown = 0;

                foreach (ElementListItemViewModel item in Items)
                {
                    decimal? mass = item.TotalMassIn(Core.Units.MassUnit.Kilogram);

                    if (mass is { } value)
                    {
                        total += value;
                    }
                    else
                    {
                        unknown++;
                    }
                }

                string text = $"{total.ToString("N3", CultureInfo.InvariantCulture)} kg " +
                              $"({(total / 1000m).ToString("N3", CultureInfo.InvariantCulture)} t)";

                return unknown == 0 ? text : text + $"  -  {unknown} item(s) have no established weight and are excluded";
            }
        }

        /// <summary>Gets a value indicating whether anything in the list needs attention.</summary>
        public bool HasWarnings => Items.Any(i => i.NeedsAttention);

        /// <summary>Adds an item to the end of the list.</summary>
        /// <param name="definition">The element to add.</param>
        public void Add(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Items.Add(new ElementListItemViewModel(definition, _context.Mass.Compute(definition)));
            Selected = Items[^1];
            _status($"Added {definition}.");
        }

        /// <summary>Replaces the selected item.</summary>
        /// <param name="definition">The element to replace it with.</param>
        public void ReplaceSelected(ElementDefinition definition)
        {
            if (Selected is null)
            {
                Add(definition);
                return;
            }

            Selected.Update(definition, _context.Mass.Compute(definition));
            RaiseTotals();
            _status($"Updated item {Selected.ItemNumber}.");
        }

        /// <summary>Checks every item and reports what would stop the list generating.</summary>
        /// <returns>A report. An empty report means the whole list is ready.</returns>
        public ValidationReport Validate()
        {
            ValidationReport report = new();

            if (Items.Count == 0)
            {
                report.AddError("List.Empty", "The list is empty. Add at least one item before generating.");
                return report;
            }

            foreach (ElementListItemViewModel item in Items)
            {
                ValidationReport itemReport = _context.Elements.Validate(item.Definition);

                foreach (ValidationMessage message in itemReport.Messages)
                {
                    report.Add(new ValidationMessage(
                        message.Severity, message.Code, $"Item {item.ItemNumber}: {message.Message}", message.Context));
                }
            }

            return report;
        }

        /// <summary>Generates items in the drawing.</summary>
        /// <param name="onlySelected">Whether to generate just the selected item.</param>
        /// <returns>What was created, or a failure carrying a user-facing reason.</returns>
        public Result<GenerationSummary> Generate(bool onlySelected)
        {
            List<ElementDefinition> definitions = onlySelected
                ? Selected is null
                    ? new List<ElementDefinition>()
                    : new List<ElementDefinition> { Selected.Definition }
                : Items.Select(i => i.Definition).ToList();

            if (definitions.Count == 0)
            {
                return Result<GenerationSummary>.Failure(
                    "List.NothingSelected",
                    onlySelected ? "Select an item to generate." : "The list is empty. Add at least one item first.");
            }

            ValidationReport validation = Validate();
            if (validation.HasErrors)
            {
                return Result<GenerationSummary>.Failure(
                    "List.Invalid",
                    "The list cannot be generated as it stands:" + Environment.NewLine + validation.ToDisplayString(),
                    validation);
            }

            GenerationRequest request = new(definitions)
            {
                CreateLayers = true,
                LayerPrefix = _context.Settings.LayerPrefix,
                ZoomToResult = false
            };

            return _cad.Generate(request);
        }

        /// <summary>Gets the elements currently in the list.</summary>
        /// <returns>The elements, in list order.</returns>
        public IReadOnlyList<ElementDefinition> Definitions() => Items.Select(i => i.Definition).ToList();

        private void Duplicate()
        {
            if (Selected is null)
            {
                return;
            }

            // A duplicate is a new element, not a second reference to the same one, so it gets a
            // fresh identity. Sharing one would make the two indistinguishable once drawn.
            ElementDefinition source = Selected.Definition;
            ElementDefinition copy = new(
                Guid.NewGuid(),
                source.CategoryId,
                source.StandardId,
                source.SectionRecordId,
                source.Designation,
                source.Dimensions,
                source.Quantity,
                source.LengthMm,
                source.DetailLevel,
                source.Representation,
                source.Placements,
                source.Material,
                source.Grade,
                source.UnitWeightKgPerM,
                source.PieceMassKg,
                source.DensityKgPerCubicMetre,
                source.Layer,
                source.Notes,
                source.DrawingReference,
                source.IsCustom,
                source.SourceDataVerified);

            int index = Items.IndexOf(Selected);
            Items.Insert(index + 1, new ElementListItemViewModel(copy, _context.Mass.Compute(copy)));
            Selected = Items[index + 1];
        }

        private void Delete()
        {
            if (Selected is null)
            {
                return;
            }

            int index = Items.IndexOf(Selected);
            Items.Remove(Selected);
            Selected = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        }

        private void MoveUp() => Move(-1);

        private void MoveDown() => Move(1);

        private void Move(int offset)
        {
            if (Selected is null)
            {
                return;
            }

            int index = Items.IndexOf(Selected);
            int target = index + offset;

            if (target < 0 || target >= Items.Count)
            {
                return;
            }

            ElementListItemViewModel item = Selected;
            Items.Move(index, target);
            Selected = item;
        }

        private void Clear()
        {
            Items.Clear();
            Selected = null;
            _status("List cleared.");
        }

        private void Renumber()
        {
            for (int index = 0; index < Items.Count; index++)
            {
                Items[index].ItemNumber = index + 1;
            }
        }

        private void RaiseTotals()
        {
            Raise(nameof(ItemCount));
            Raise(nameof(TotalPieces));
            Raise(nameof(TotalWeight));
            Raise(nameof(HasWarnings));
            RelayCommand.RaiseCanExecuteChanged();
        }
    }
}
