using System;
using System.Windows.Input;

namespace StructureGen.UI.ViewModels
{
    /// <summary>An <see cref="ICommand"/> backed by delegates.</summary>
    /// <remarks>
    /// Enough command plumbing for a palette, without taking a dependency on an MVVM framework.
    /// The plugin loads into AutoCAD's process, so every assembly it does not bring with it is one
    /// less chance of colliding with a different version loaded by something else.
    /// </remarks>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>Initialises a new instance of the <see cref="RelayCommand"/> class.</summary>
        /// <param name="execute">What the command does.</param>
        /// <param name="canExecute">Whether it is currently available.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>Initialises a new instance of the <see cref="RelayCommand"/> class.</summary>
        /// <param name="execute">What the command does.</param>
        /// <param name="canExecute">Whether it is currently available.</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(
                _ => (execute ?? throw new ArgumentNullException(nameof(execute)))(),
                canExecute is null ? null : _ => canExecute())
        {
        }

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

        /// <inheritdoc />
        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>Asks the command system to re-evaluate every command's availability.</summary>
        public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
