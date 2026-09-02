using System.Globalization;
using StructureGen.Core.Model;

namespace StructureGen.UI.ViewModels
{
    /// <summary>One row of the parameter grid: a dimension, its value and whether it may be edited.</summary>
    /// <remarks>
    /// The three-way distinction the application makes between a standardised dimension, one the
    /// standard leaves open and a property of the occurrence is surfaced here, and it is what the
    /// grid binds its read-only state and its explanatory text to. A user cannot edit a standardised
    /// dimension by accident because the control refuses; if they construct one anyway the validator
    /// rejects it as well.
    /// </remarks>
    public sealed class ParameterRowViewModel : ViewModelBase
    {
        private decimal _value;
        private bool _customMode;

        /// <summary>Initialises a new instance of the <see cref="ParameterRowViewModel"/> class.</summary>
        /// <param name="descriptor">What the parameter is.</param>
        /// <param name="value">Its current value, in millimetres.</param>
        public ParameterRowViewModel(ParameterDescriptor descriptor, decimal value)
        {
            Descriptor = descriptor;
            _value = value;
            StandardValue = value;
        }

        /// <summary>Gets what the parameter is.</summary>
        public ParameterDescriptor Descriptor { get; }

        /// <summary>Gets the parameter key.</summary>
        public string Key => Descriptor.Key;

        /// <summary>Gets the label shown to the user.</summary>
        public string DisplayName => Descriptor.DisplayName;

        /// <summary>Gets the value the standard publishes, which the user may return to.</summary>
        public decimal StandardValue { get; }

        /// <summary>Gets or sets the current value, in millimetres.</summary>
        public decimal Value
        {
            get => _value;
            set
            {
                if (Set(ref _value, value))
                {
                    Raise(nameof(IsChangedFromStandard));
                    Raise(nameof(StatusText));
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether the element is in custom mode.</summary>
        public bool CustomMode
        {
            get => _customMode;
            set
            {
                if (Set(ref _customMode, value))
                {
                    Raise(nameof(IsEditable));
                    Raise(nameof(StatusText));
                }
            }
        }

        /// <summary>Gets a value indicating whether the user may change this value right now.</summary>
        public bool IsEditable =>
            Descriptor.Mutability != ParameterMutability.StandardLocked || CustomMode;

        /// <summary>Gets a value indicating whether the value differs from the published one.</summary>
        public bool IsChangedFromStandard => Value != StandardValue;

        /// <summary>Gets a short explanation of the parameter's state, shown beside it.</summary>
        public string StatusText => Descriptor.Mutability switch
        {
            ParameterMutability.StandardLocked when !CustomMode => "fixed by standard",
            ParameterMutability.StandardLocked => IsChangedFromStandard
                ? $"CHANGED from {StandardValue.ToString("0.###", CultureInfo.InvariantCulture)}"
                : "unlocked (custom mode)",
            ParameterMutability.Adjustable => "adjustable",
            _ => "this occurrence"
        };

        /// <summary>Returns the value to its published one.</summary>
        public void ResetToStandard() => Value = StandardValue;
    }
}
