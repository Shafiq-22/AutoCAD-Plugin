using System;
using Autodesk.AutoCAD.Runtime;

// Autodesk.AutoCAD.Runtime declares its own Exception type, so every catch of the framework's one
// in this file is written out in full rather than left ambiguous.
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Results;
using StructureGen.Data;
using StructureGen.Services;

namespace StructureGen.Acad
{
    /// <summary>
    /// The plugin's entry point. AutoCAD creates one instance of this when the assembly loads and
    /// calls <see cref="Initialize"/>; everything else hangs off what it builds.
    /// </summary>
    /// <remarks>
    /// The object graph is held in a static because AutoCAD's command methods are static entry
    /// points and there is no other way to reach an instance from them. It is the only piece of
    /// process-wide state in the plugin, it is created exactly once, and it is disposed in
    /// <see cref="Terminate"/>. Everything below it takes its dependencies through its constructor,
    /// so nothing else reaches for a global.
    /// </remarks>
    public sealed class StructureGenApplication : IExtensionApplication
    {
        private static StructureGenContext? _context;
        private static AcadCadService? _cadService;

        /// <summary>Gets the application object graph.</summary>
        /// <exception cref="InvalidOperationException">The plugin has not finished loading.</exception>
        public static StructureGenContext Context =>
            _context ?? throw new InvalidOperationException(
                "StructureGen has not finished loading. If this persists, check the StructureGen log file.");

        /// <summary>Gets the CAD service the user interface talks to.</summary>
        /// <exception cref="InvalidOperationException">The plugin has not finished loading.</exception>
        public static ICadService CadService =>
            _cadService ?? throw new InvalidOperationException(
                "StructureGen has not finished loading. If this persists, check the StructureGen log file.");

        /// <summary>Gets a value indicating whether the plugin loaded successfully.</summary>
        public static bool IsReady => _context is not null && _cadService is not null;

        /// <summary>Gets the message describing why loading failed, when it did.</summary>
        public static string? StartupError { get; private set; }

        /// <inheritdoc />
        public void Initialize()
        {
            try
            {
                _context = StructureGenContext.Create();
                _cadService = new AcadCadService(_context);

                ReportStartupDatabaseState(_context);

                _context.Logger.Information("StructureGen loaded.");
            }
            catch (System.Exception exception)
            {
                // A plugin that throws out of Initialize is reported by AutoCAD with a message the
                // user cannot act on. Catching here means they get an explanation instead, and the
                // detail lands in a log file that can be sent on.
                StartupError = exception.Message;

                try
                {
                    new FileLogger().Error("StructureGen failed to load.", exception);
                }
                catch (System.Exception)
                {
                    // If even logging fails there is nothing further to try.
                }

                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
                    "StructureGen could not start." + Environment.NewLine + Environment.NewLine +
                    exception.Message + Environment.NewLine + Environment.NewLine +
                    "The commands will not be available. Technical detail has been written to the StructureGen log " +
                    "file in your local application data folder.");
            }
        }

        /// <inheritdoc />
        public void Terminate()
        {
            _context?.Logger.Information("StructureGen unloading.");
            _context?.Dispose();
            _context = null;
            _cadService = null;
        }

        private static void ReportStartupDatabaseState(StructureGenContext context)
        {
            Result<StructuralDatabase>? load = context.StartupLoadResult;

            if (load is null)
            {
                return;
            }

            if (load.IsFailure)
            {
                // The palette still opens; it simply has nothing to offer until a database is
                // pointed at. Failing loudly here would stop the plugin loading over a data problem.
                context.Logger.Warning($"The structural database did not load at start-up: {load.ErrorMessage}");
                return;
            }

            if (load.Report is { } report && report.Messages.Count > 0)
            {
                context.Logger.Information(
                    "Structural database load report:" + Environment.NewLine + report.ToDisplayString(int.MaxValue));
            }
        }
    }
}
