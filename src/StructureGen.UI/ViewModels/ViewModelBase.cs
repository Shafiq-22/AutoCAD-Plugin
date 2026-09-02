using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StructureGen.UI.ViewModels
{
    /// <summary>The change-notification plumbing every view model needs.</summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/>.</summary>
        /// <param name="propertyName">The property that changed. Supplied by the compiler.</param>
        protected void Raise([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Assigns a field and raises <see cref="PropertyChanged"/> when the value actually changed.</summary>
        /// <typeparam name="T">The field's type.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The property being set. Supplied by the compiler.</param>
        /// <returns><see langword="true"/> when the value changed.</returns>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            Raise(propertyName);
            return true;
        }
    }
}
