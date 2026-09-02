#nullable enable
namespace System.Windows.Input
{
    public interface ICommand
    {
        event System.EventHandler? CanExecuteChanged;
        bool CanExecute(object? parameter);
        void Execute(object? parameter);
    }

    public static class CommandManager
    {
        public static event System.EventHandler? RequerySuggested;
        public static void InvalidateRequerySuggested() { RequerySuggested?.Invoke(null, System.EventArgs.Empty); }
    }
}

namespace System.Windows.Controls
{
    // The palette view is XAML-backed and cannot be compiled here, so a stand-in stands in for it.
    public class UserControl { public object? DataContext { get; set; } }
}

namespace StructureGen.UI.Views
{
    using StructureGen.UI.ViewModels;

    public class StructureGenView : System.Windows.Controls.UserControl
    {
        public StructureGenView() { }
        public StructureGenView(MainViewModel viewModel) { DataContext = viewModel; }
        public MainViewModel? ViewModel => DataContext as MainViewModel;
    }
}
