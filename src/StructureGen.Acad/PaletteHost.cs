using System;
using Autodesk.AutoCAD.Windows;
using StructureGen.Core.Model;
using StructureGen.Services;
using StructureGen.UI.ViewModels;
using StructureGen.UI.Views;

namespace StructureGen.Acad
{
    /// <summary>Owns the dockable palette the plugin's user interface lives in.</summary>
    /// <remarks>
    /// A <see cref="PaletteSet"/> is the right home for a tool a detailer keeps open while working:
    /// it docks, it remembers where it was put, and it does not block the command line the way a
    /// modal dialog would. The palette is created once and reshown thereafter, so the working list
    /// survives being closed and reopened.
    ///
    /// The palette is modeless, which means its events arrive outside AutoCAD's command context
    /// where document edits are not allowed. Everything that touches the drawing goes through
    /// <see cref="ICadService"/>, whose implementation takes a document lock, which is what makes
    /// that safe.
    /// </remarks>
    internal static class PaletteHost
    {
        /// <summary>
        /// The palette's identity. It must not change: AutoCAD stores the docked position and size
        /// against it, and a new value would lose the user's arrangement on every upgrade.
        /// </summary>
        private static readonly Guid PaletteId = new("8E5C0B62-9E2F-4E5A-9C7E-2C7F1B4A6D31");

        private static PaletteSet? _paletteSet;
        private static MainViewModel? _viewModel;

        /// <summary>Creates the palette if necessary and brings it to the front.</summary>
        /// <param name="context">The application object graph.</param>
        /// <param name="cad">The CAD host.</param>
        public static void Show(StructureGenContext context, ICadService cad)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_paletteSet is null)
            {
                _viewModel = new MainViewModel(context, cad);

                StructureGenView view = new(_viewModel);

                _paletteSet = new PaletteSet("StructureGen", PaletteId)
                {
                    Style = PaletteSetStyles.ShowPropertiesMenu
                            | PaletteSetStyles.ShowAutoHideButton
                            | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new System.Drawing.Size(460, 420),
                    KeepFocus = false
                };

                _paletteSet.AddVisual("StructureGen", view);
            }

            _paletteSet.Visible = true;
        }

        /// <summary>Loads an element from the drawing into the palette's editor.</summary>
        /// <param name="definition">The element to amend.</param>
        /// <param name="handle">The handle of the object it came from.</param>
        public static void LoadForEditing(ElementDefinition definition, string handle) =>
            _viewModel?.LoadForEditing(definition, handle);

        /// <summary>Refreshes everything in the palette that depends on the loaded database.</summary>
        public static void RefreshDatabase() => _viewModel?.RefreshDatabase();

        /// <summary>Brings the settings tab to the front.</summary>
        public static void ShowSettings() => _viewModel?.ShowSettings();

        /// <summary>Hides the palette without discarding its state.</summary>
        public static void Hide()
        {
            if (_paletteSet is not null)
            {
                _paletteSet.Visible = false;
            }
        }
    }
}
