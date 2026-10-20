using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Data;
using StructureGen.Services;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadException = Autodesk.AutoCAD.Runtime.Exception;

namespace StructureGen.Acad
{
    /// <summary>The commands StructureGen adds to AutoCAD.</summary>
    /// <remarks>
    /// Every command follows the same shape: check the plugin loaded, do the work, and report the
    /// outcome on the command line in language a drafter can act on. No command lets an exception
    /// escape into AutoCAD, because an unhandled exception in a command method shows the user a
    /// stack trace and can leave the drawing in a partly modified state.
    /// </remarks>
    public sealed class StructureGenCommands
    {
        /// <summary>Opens the StructureGen palette.</summary>
        [CommandMethod("STRUCTUREGEN", CommandFlags.Modal)]
        public void ShowPalette() => Run("STRUCTUREGEN", editor =>
        {
            PaletteHost.Show(StructureGenApplication.Context, StructureGenApplication.CadService);
            editor.WriteMessage("\nStructureGen palette opened.");
        });

        /// <summary>Selects a generated element and opens it in the palette for editing.</summary>
        [CommandMethod("SGEDIT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void EditElement() => Run("SGEDIT", editor =>
        {
            Result<(ElementDefinition Definition, string Handle)> picked =
                StructureGenApplication.CadService.PickElement("Select a StructureGen element to edit");

            if (picked.IsFailure)
            {
                editor.WriteMessage($"\n{picked.ErrorMessage}");
                return;
            }

            PaletteHost.Show(StructureGenApplication.Context, StructureGenApplication.CadService);
            PaletteHost.LoadForEditing(picked.Value.Definition, picked.Value.Handle);

            editor.WriteMessage(
                $"\nLoaded '{picked.Value.Definition.Designation}' into the palette. " +
                "Change what you need and choose Update to rebuild it.");
        });

        /// <summary>Reports everything StructureGen knows about a selected element.</summary>
        [CommandMethod("SGINFO", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ElementInfo() => Run("SGINFO", editor =>
        {
            Result<(ElementDefinition Definition, string Handle)> picked =
                StructureGenApplication.CadService.PickElement("Select a StructureGen element");

            if (picked.IsFailure)
            {
                editor.WriteMessage($"\n{picked.ErrorMessage}");
                return;
            }

            ElementDefinition definition = picked.Value.Definition;
            MassProperties mass = StructureGenApplication.Context.Mass.Compute(definition);

            editor.WriteMessage($"\n--- StructureGen element ({picked.Value.Handle}) ---");
            editor.WriteMessage($"\n  Element id   : {definition.ElementId:D}");
            editor.WriteMessage($"\n  Category     : {CategoryCatalog.FindOrGeneric(definition.CategoryId).DisplayName}");
            editor.WriteMessage($"\n  Standard     : {definition.StandardId}");
            editor.WriteMessage($"\n  Profile      : {definition.Designation}{(definition.IsCustom ? "  (NON-STANDARD)" : string.Empty)}");
            editor.WriteMessage($"\n  Database id  : {definition.SectionRecordId}");
            editor.WriteMessage($"\n  Dimensions   : {DimensionSummaryFormatter.Describe(definition)} mm");

            if (definition.LengthMm is { } length)
            {
                editor.WriteMessage($"\n  Length       : {length.ToString("0.###", CultureInfo.InvariantCulture)} mm");
            }

            editor.WriteMessage($"\n  Material     : {definition.Material ?? "-"} {definition.Grade ?? string.Empty}");
            editor.WriteMessage($"\n  Detail level : {definition.DetailLevel}");
            editor.WriteMessage($"\n  Weight       : {mass.Explanation}");
            editor.WriteMessage($"\n  Source data  : {(definition.SourceDataVerified ? "verified" : "NOT VERIFIED")}");
            editor.WriteMessage($"\n  Created      : {definition.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        });

        /// <summary>Brings every StructureGen element in the drawing into view.</summary>
        /// <remarks>
        /// This exists because "I generated something and the drawing still looks empty" has two
        /// quite different causes and the user cannot tell them apart. Either nothing was created,
        /// or something was created a long way outside the current view - which is the normal
        /// outcome of generating a six metre member at the world origin in a drawing whose initial
        /// view is a few hundred millimetres wide. This command reports the count, so the first case
        /// is named outright, and moves the view, so the second one is over.
        /// </remarks>
        [CommandMethod("SGSHOW", CommandFlags.Modal)]
        public void ShowElements() => Run("SGSHOW", editor =>
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            Result<Extents3d> framed = AcadCadService.FrameGeneratedElements(document, out int count);

            if (count == 0)
            {
                editor.WriteMessage(
                    "\nThis drawing contains no StructureGen elements. Nothing has been generated into it yet, " +
                    "or the elements were erased. Open the palette with STRUCTUREGEN, add an item to the list " +
                    "and choose Generate All.");
                return;
            }

            if (framed.IsFailure)
            {
                editor.WriteMessage($"\n{count} StructureGen element(s) found, but {framed.ErrorMessage}");
                return;
            }

            Point3d minimum = framed.Value.MinPoint;
            Point3d maximum = framed.Value.MaxPoint;

            editor.WriteMessage(
                $"\n{count} StructureGen element(s) found, spanning X {minimum.X:0.#} to {maximum.X:0.#}, " +
                $"Y {minimum.Y:0.#} to {maximum.Y:0.#}, Z {minimum.Z:0.#} to {maximum.Z:0.#} in drawing units. " +
                "The view has been moved to show them.");
        });

        /// <summary>Builds a bill of materials from the drawing and offers what to do with it.</summary>
        [CommandMethod("SGBOM", CommandFlags.Modal)]
        public void BillOfMaterials() => Run("SGBOM", editor =>
        {
            Result<BomDocument> built = BuildFromDrawing(editor, HarvestScope.ModelSpace);
            if (built.IsFailure)
            {
                editor.WriteMessage($"\n{built.ErrorMessage}");
                return;
            }

            BomDocument bom = built.Value;
            ReportSummary(editor, bom);

            PromptKeywordOptions options = new("\nWhat next?");
            options.Keywords.Add("Model");
            options.Keywords.Add("Layout");
            options.Keywords.Add("Excel");
            options.Keywords.Add("Nothing");
            options.Keywords.Default = "Model";
            options.AllowNone = true;

            PromptResult choice = editor.GetKeywords(options);
            if (choice.Status != PromptStatus.OK)
            {
                return;
            }

            switch (choice.StringResult)
            {
                case "Model":
                    InsertTable(editor, bom, BomTablePlacement.ModelSpace);
                    break;

                case "Layout":
                    InsertTable(editor, bom, BomTablePlacement.CurrentLayout);
                    break;

                case "Excel":
                    ExportWorkbook(editor, bom);
                    break;

                default:
                    editor.WriteMessage("\nNothing was created.");
                    break;
            }
        });

        /// <summary>
        /// Draws two-dimensional plan drawings of the generated elements: either a general
        /// arrangement plan of everything in model space where it actually is, or a sheet with one
        /// plan per distinct item.
        /// </summary>
        [CommandMethod("SGPLAN", CommandFlags.Modal)]
        public void DrawPlan() => Run("SGPLAN", editor =>
        {
            PromptKeywordOptions options = new("\nPlan drawing of");
            options.Keywords.Add("Arrangement");
            options.Keywords.Add("Items");
            options.Keywords.Default = "Arrangement";
            options.AllowNone = true;

            PromptResult choice = editor.GetKeywords(options);
            if (choice.Status != PromptStatus.OK)
            {
                return;
            }

            PlanLayout layout = choice.StringResult == "Items" ? PlanLayout.Items : PlanLayout.Arrangement;

            Result<PlanDrawingOutcome> drawn = StructureGenApplication.CadService.DrawPlan(
                new PlanDrawingOptions { Layout = layout }, HarvestScope.ModelSpace);

            if (drawn.IsFailure)
            {
                editor.WriteMessage($"\n{drawn.ErrorMessage}");
                return;
            }

            editor.WriteMessage($"\n{drawn.Value.ToDisplayString()}");

            if (drawn.Report is { } report)
            {
                foreach (ValidationMessage message in report.Messages.Where(m => m.Severity >= Severity.Warning).Take(10))
                {
                    editor.WriteMessage($"\n  ! {message.Message}");
                }
            }
        });

        /// <summary>Builds a bill of materials from the drawing and exports it straight to a workbook.</summary>
        [CommandMethod("SGBOMEXPORT", CommandFlags.Modal)]
        public void ExportBillOfMaterials() => Run("SGBOMEXPORT", editor =>
        {
            Result<BomDocument> built = BuildFromDrawing(editor, HarvestScope.EntireDrawing);
            if (built.IsFailure)
            {
                editor.WriteMessage($"\n{built.ErrorMessage}");
                return;
            }

            ReportSummary(editor, built.Value);
            ExportWorkbook(editor, built.Value);
        });

        /// <summary>Rebuilds the geometry of selected elements from the parameters stored on them.</summary>
        [CommandMethod("SGUPDATE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void UpdateElements() => Run("SGUPDATE", editor =>
        {
            Result<IReadOnlyList<BomEntry>> harvested =
                StructureGenApplication.CadService.HarvestElements(HarvestScope.Selection);

            if (harvested.IsFailure)
            {
                editor.WriteMessage($"\n{harvested.ErrorMessage}");
                return;
            }

            if (harvested.Value.Count == 0)
            {
                editor.WriteMessage("\nNo StructureGen elements were selected.");
                return;
            }

            int updated = 0;
            int failed = 0;

            foreach (BomEntry entry in harvested.Value)
            {
                if (entry.Handle is null)
                {
                    continue;
                }

                Result<string> result =
                    StructureGenApplication.CadService.UpdateElement(entry.Handle, entry.Definition);

                if (result.IsSuccess)
                {
                    updated++;
                }
                else
                {
                    failed++;
                    editor.WriteMessage($"\n  {entry.Definition.Designation}: {result.ErrorMessage}");
                }
            }

            editor.WriteMessage(
                $"\nRegenerated {updated} element(s)" + (failed > 0 ? $"; {failed} could not be updated." : "."));
        });

        /// <summary>Reloads the structural database from disk without restarting AutoCAD.</summary>
        [CommandMethod("SGRELOADDB", CommandFlags.Modal)]
        public void ReloadDatabase() => Run("SGRELOADDB", editor =>
        {
            StructureGenContext context = StructureGenApplication.Context;
            Result<StructuralDatabase> result = context.Database.Load(context.Settings.DatabasePath);

            if (result.IsFailure)
            {
                editor.WriteMessage($"\n{result.ErrorMessage}");
                editor.WriteMessage("\nThe previously loaded data is still in use.");
                return;
            }

            StructuralDatabase database = result.Value;
            editor.WriteMessage($"\nLoaded '{database.Metadata.Stamp}' from:\n  {context.Database.LoadedPath}");
            editor.WriteMessage(
                $"\n  {database.Standards.Count} standard(s), {database.Materials.Count} material(s), " +
                $"{database.Sections.Count} section(s) in {database.Categories.Count} category/categories.");

            if (database.UnverifiedRecordCount > 0)
            {
                editor.WriteMessage(
                    $"\n  NOTE: {database.UnverifiedRecordCount} of {database.Sections.Count} record(s) are not " +
                    "marked as verified. Check them against your authoritative source before relying on the output.");
            }

            if (result.Report is { } report && report.Messages.Count > 0)
            {
                foreach (ValidationMessage message in report.Messages.Where(m => m.Severity >= Severity.Warning).Take(10))
                {
                    editor.WriteMessage($"\n  {message}");
                }
            }

            PaletteHost.RefreshDatabase();
        });

        /// <summary>Reports where StructureGen is installed, what it loaded and which AutoCAD it is running in.</summary>
        [CommandMethod("SGABOUT", CommandFlags.Modal)]
        public void About() => Run("SGABOUT", editor =>
        {
            StructureGenContext context = StructureGenApplication.Context;

            editor.WriteMessage($"\nStructureGen {PluginPaths.PluginVersion}");
            editor.WriteMessage($"\n  Host            : {AcadApplication.GetSystemVariable("ACADVER")} " +
                                $"({AcadApplication.GetSystemVariable("PRODUCT")})");
            editor.WriteMessage($"\n  Plugin folder   : {PluginPaths.AssemblyDirectory}");
            editor.WriteMessage($"\n  Data folder     : {PluginPaths.DataDirectory}");
            editor.WriteMessage($"\n  Settings file   : {context.SettingsStore.SettingsFilePath}");
            editor.WriteMessage($"\n  Log folder      : {FileLoggerDirectory()}");
            editor.WriteMessage($"\n  Section database: {context.Database.Stamp}");
            editor.WriteMessage($"\n  Database file   : {context.Database.LoadedPath ?? "(none loaded)"}");
            editor.WriteMessage(
                "\n\n  StructureGen is a drafting and documentation aid. It performs no structural engineering" +
                "\n  design, analysis or verification. Profile dimensions and weights must be verified against" +
                "\n  your authoritative copy of the relevant standard before use.");
        });

        /// <summary>Opens the settings, which are edited on the palette's settings tab.</summary>
        [CommandMethod("SGSETTINGS", CommandFlags.Modal)]
        public void Settings() => Run("SGSETTINGS", editor =>
        {
            PaletteHost.Show(StructureGenApplication.Context, StructureGenApplication.CadService);
            PaletteHost.ShowSettings();
            editor.WriteMessage("\nStructureGen settings opened on the palette.");
        });

        private static Result<BomDocument> BuildFromDrawing(Editor editor, HarvestScope scope)
        {
            StructureGenContext context = StructureGenApplication.Context;

            Result<IReadOnlyList<BomEntry>> harvested = StructureGenApplication.CadService.HarvestElements(scope);
            if (harvested.IsFailure)
            {
                return Result<BomDocument>.Failure(harvested.ErrorCode!, harvested.ErrorMessage!);
            }

            if (harvested.Value.Count == 0)
            {
                return Result<BomDocument>.Failure(
                    "Bom.NoElements",
                    "No StructureGen elements were found in the drawing. Use STRUCTUREGEN to create some first.");
            }

            BomOptions options = BomService.OptionsFrom(context.Settings);
            options.DrawingReference = Path.GetFileNameWithoutExtension(
                StructureGenApplication.CadService.DocumentName ?? string.Empty);

            BomDocument bom = context.Bom.Build(
                harvested.Value, options, $"{scope} of {Path.GetFileName(StructureGenApplication.CadService.DocumentName ?? "the drawing")}");

            context.Bom.LogSummary(bom);
            return Result<BomDocument>.Success(bom);
        }

        private static void ReportSummary(Editor editor, BomDocument bom)
        {
            editor.WriteMessage(
                $"\nBill of materials: {bom.Totals.LineCount} line(s), {bom.Totals.TotalPieces} piece(s), " +
                $"{bom.Totals.TotalMassKg.ToString("N2", CultureInfo.InvariantCulture)} kg " +
                $"({bom.Totals.TotalMassTonnes.ToString("N3", CultureInfo.InvariantCulture)} t).");

            foreach (string caveat in bom.Caveats())
            {
                editor.WriteMessage($"\n  ! {caveat}");
            }
        }

        private static void InsertTable(Editor editor, BomDocument bom, BomTablePlacement placement)
        {
            Result<string> inserted = StructureGenApplication.CadService.InsertBomTable(bom, placement);

            editor.WriteMessage(inserted.IsSuccess
                ? $"\nSchedule table created ({inserted.Value})."
                : $"\n{inserted.ErrorMessage}");
        }

        private static void ExportWorkbook(Editor editor, BomDocument bom)
        {
            StructureGenContext context = StructureGenApplication.Context;

            string suggested = context.Bom.SuggestExportPath(
                StructureGenApplication.CadService.DocumentName, context.Settings.LastExportDirectory);

            PromptSaveFileOptions options = new("Export bill of materials")
            {
                Filter = "Excel workbook (*.xlsx)|*.xlsx",
                InitialFileName = Path.GetFileName(suggested),
                InitialDirectory = Path.GetDirectoryName(suggested) ?? string.Empty,
                DialogName = "StructureGen - export bill of materials"
            };

            PromptFileNameResult chosen = editor.GetFileNameForSave(options);
            if (chosen.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nExport cancelled.");
                return;
            }

            Result<string> exported = context.Bom.Export(bom, chosen.StringResult);

            if (exported.IsFailure)
            {
                editor.WriteMessage($"\n{exported.ErrorMessage}");
                return;
            }

            context.Settings.LastExportDirectory = Path.GetDirectoryName(exported.Value);
            context.ApplySettings(context.Settings);

            editor.WriteMessage($"\nExported to {exported.Value}");
        }

        private static string FileLoggerDirectory()
        {
            try
            {
                return FileLogger.DefaultDirectory();
            }
            catch (ArgumentException)
            {
                return "(unavailable)";
            }
        }

        /// <summary>
        /// Runs a command body, turning anything that escapes into a message the user can act on.
        /// </summary>
        /// <param name="commandName">The command being run, used in the log.</param>
        /// <param name="body">The work to do.</param>
        private static void Run(string commandName, Action<Editor> body)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

            if (document is null)
            {
                AcadApplication.ShowAlertDialog(
                    "StructureGen needs an open drawing.\n\nOpen or create a drawing and run the command again.");
                return;
            }

            Editor editor = document.Editor;

            if (!StructureGenApplication.IsReady)
            {
                editor.WriteMessage(
                    "\nStructureGen did not load correctly, so this command is not available." +
                    (StructureGenApplication.StartupError is { } error ? $"\n  {error}" : string.Empty) +
                    "\n  See the StructureGen log file for technical detail.");
                return;
            }

            try
            {
                body(editor);
            }
            catch (AcadException exception)
            {
                StructureGenApplication.Context.Logger.Error($"{commandName} failed inside AutoCAD.", exception);
                editor.WriteMessage(
                    $"\n{commandName} could not be completed. AutoCAD reported: {exception.Message}" +
                    "\nTechnical detail has been written to the StructureGen log file.");
            }
            catch (System.Exception exception)
            {
                // Nothing is allowed to escape a command method. AutoCAD would show the user a raw
                // stack trace, and on some failures leave the command stack in a confusing state.
                StructureGenApplication.Context.Logger.Error($"{commandName} failed.", exception);
                editor.WriteMessage(
                    $"\n{commandName} could not be completed: {exception.Message}" +
                    "\nTechnical detail has been written to the StructureGen log file.");
            }
        }
    }
}
