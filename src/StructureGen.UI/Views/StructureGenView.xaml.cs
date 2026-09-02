using System.Windows.Controls;
using StructureGen.UI.ViewModels;

namespace StructureGen.UI.Views
{
    /// <summary>The StructureGen palette.</summary>
    /// <remarks>
    /// A plain view over <see cref="MainViewModel"/>. It holds no logic of its own so that the whole
    /// of the palette's behaviour lives in view models that can be exercised without a window.
    /// </remarks>
    public partial class StructureGenView : UserControl
    {
        /// <summary>Initialises a new instance of the <see cref="StructureGenView"/> class.</summary>
        public StructureGenView() => InitializeComponent();

        /// <summary>Initialises a new instance of the <see cref="StructureGenView"/> class bound to a view model.</summary>
        /// <param name="viewModel">The view model to bind to.</param>
        public StructureGenView(MainViewModel viewModel)
            : this() => DataContext = viewModel;

        /// <summary>Gets the view model the palette is bound to.</summary>
        public MainViewModel? ViewModel => DataContext as MainViewModel;
    }
}
