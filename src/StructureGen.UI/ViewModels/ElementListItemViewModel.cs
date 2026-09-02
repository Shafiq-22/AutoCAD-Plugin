using System;
using System.Globalization;
using StructureGen.Core.Model;
using StructureGen.Core.Units;

namespace StructureGen.UI.ViewModels
{
    /// <summary>One row of the working list, before anything is committed to the drawing.</summary>
    public sealed class ElementListItemViewModel : ViewModelBase
    {
        private ElementDefinition _definition;
        private MassProperties _mass;
        private int _itemNumber;

        /// <summary>Initialises a new instance of the <see cref="ElementListItemViewModel"/> class.</summary>
        /// <param name="definition">The element this row represents.</param>
        /// <param name="mass">Its computed weight.</param>
        public ElementListItemViewModel(ElementDefinition definition, MassProperties mass)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _mass = mass ?? throw new ArgumentNullException(nameof(mass));
        }

        /// <summary>Gets the element this row represents.</summary>
        public ElementDefinition Definition => _definition;

        /// <summary>Gets or sets the position of this row in the list, counting from one.</summary>
        public int ItemNumber
        {
            get => _itemNumber;
            set => Set(ref _itemNumber, value);
        }

        /// <summary>Gets the display name of the element category.</summary>
        public string Category => CategoryCatalog.FindOrGeneric(_definition.CategoryId).DisplayName;

        /// <summary>Gets the standard the profile comes from.</summary>
        public string Standard => _definition.StandardId;

        /// <summary>Gets the profile designation, marked when it departs from the standard.</summary>
        public string Profile =>
            _definition.Designation + (_definition.IsCustom ? "  (non-standard)" : string.Empty);

        /// <summary>Gets the member length in millimetres, or a dash when the category has none.</summary>
        public string Length => _definition.LengthMm is { } value
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "-";

        /// <summary>Gets the number of pieces.</summary>
        public int Quantity => _definition.Quantity;

        /// <summary>Gets the mass per metre, or a dash when it does not apply.</summary>
        public string UnitWeight => _mass.UnitWeightKgPerM is { } value
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "-";

        /// <summary>Gets the total mass in kilograms, or a dash when it could not be established.</summary>
        public string TotalWeight => _mass.TotalMassKg is { } value
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "-";

        /// <summary>Gets how the weight was arrived at, shown as a tool tip.</summary>
        public string WeightExplanation => _mass.Explanation;

        /// <summary>Gets a value indicating whether this row needs the user's attention.</summary>
        public bool NeedsAttention => _definition.IsCustom || !_definition.SourceDataVerified || !_mass.IsKnown;

        /// <summary>Gets the reason this row needs attention, or an empty string when it does not.</summary>
        public string AttentionReason
        {
            get
            {
                if (!_mass.IsKnown)
                {
                    return "No weight could be established for this item; it is excluded from the totals.";
                }

                if (_definition.IsCustom)
                {
                    return "This item has been edited away from its standard profile.";
                }

                return _definition.SourceDataVerified
                    ? string.Empty
                    : "The source data for this profile is not marked as verified.";
            }
        }

        /// <summary>Gets the detail level the geometry will be generated at.</summary>
        public DetailLevel DetailLevel => _definition.DetailLevel;

        /// <summary>Gets the layer the element will be placed on.</summary>
        public string Layer => _definition.Layer ?? "(automatic)";

        /// <summary>Replaces the element this row represents.</summary>
        /// <param name="definition">The new element.</param>
        /// <param name="mass">Its computed weight.</param>
        public void Update(ElementDefinition definition, MassProperties mass)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _mass = mass ?? throw new ArgumentNullException(nameof(mass));

            Raise(nameof(Definition));
            Raise(nameof(Category));
            Raise(nameof(Standard));
            Raise(nameof(Profile));
            Raise(nameof(Length));
            Raise(nameof(Quantity));
            Raise(nameof(UnitWeight));
            Raise(nameof(TotalWeight));
            Raise(nameof(WeightExplanation));
            Raise(nameof(NeedsAttention));
            Raise(nameof(AttentionReason));
            Raise(nameof(DetailLevel));
            Raise(nameof(Layer));
        }

        /// <summary>Expresses the total mass in another unit.</summary>
        /// <param name="unit">The unit to express it in.</param>
        /// <returns>The converted mass, or <see langword="null"/> when it is unknown.</returns>
        public decimal? TotalMassIn(MassUnit unit) =>
            _mass.TotalMassKg is { } value ? UnitConverter.FromKilograms(value, unit) : null;
    }
}
