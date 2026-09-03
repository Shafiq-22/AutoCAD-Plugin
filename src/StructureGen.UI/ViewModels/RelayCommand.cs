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

        /// <summary>
        /// Gets or sets what to do with an exception that escapes a command.
        /// </summary>
        /// <remarks>
        /// This exists because of where the palette runs. A command method has a catch-all around
        /// it, so a failure there is reported; a button on a modeless palette had nothing. An
        /// exception thrown under a button click goes to the WPF dispatcher, and inside AutoCAD's
        /// palette host it is routinely swallowed without a word - while the transaction underneath
        /// is disposed without committing and rolls the whole operation back. The user is left with
        /// a drawing that gained nothing and a palette that said nothing, which is the least
        /// diagnosable failure the plugin can have.
        ///
        /// It is deliberately static. The policy belongs to the palette as a whole rather than to
        /// any one view model, and every command in every tab has to be covered by it or the gap
        /// simply moves. The composition root installs it once at start-up.
        ///
        /// While no handler is installed the exception propagates untouched, so tests and any host
        /// with its own reporting see the real failure rather than a swallowed one.
        /// </remarks>
        public static Action<Exception>? UnhandledException { get; set; }

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
        public void Execute(object? parameter)
        {
            try
            {
                _execute(parameter);
            }
            catch (Exception exception) when (UnhandledException is not null)
            {
                // The filter matters: with no handler installed this catch does not run at all and
                // the exception keeps its original stack, rather than being caught and rethrown.
                UnhandledException(exception);
            }
        }

        /// <summary>Asks the command system to re-evaluate every command's availability.</summary>
        public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
