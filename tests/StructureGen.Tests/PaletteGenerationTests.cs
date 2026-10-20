using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Services;
using StructureGen.UI.ViewModels;
using Xunit;

namespace StructureGen.Tests
{
    /// <summary>
    /// Covers what the palette does about the outcome of a generation run.
    /// </summary>
    /// <remarks>
    /// A run can fail per item without failing overall: an item whose category has no generator, or
    /// whose outline the geometry engine refuses, is reported and skipped, and the run returns
    /// successfully having drawn nothing. That is the case worth testing, because it is the one that
    /// looks to a user exactly like the palette doing nothing at all - and the reasons were, until
    /// this was fixed, written only to the AutoCAD command line, which is frequently collapsed.
    /// </remarks>
    public class PaletteGenerationTests
    {
        [Fact]
        public void A_run_that_draws_nothing_says_so_in_a_dialog_and_names_the_reason()
        {
            ValidationReport report = new();
            report.AddError(
                "Cad.NoGenerator",
                "'IPE 300' was skipped: no geometry generator is registered for the category 'i-section'.");

            FakeCadService cad = Cad(EmptyRun(report));
            MainViewModel palette = Palette(cad);

            palette.GenerateAllCommand.Execute(null);

            (string Title, string Message) dialog = Assert.Single(cad.Dialogs);
            Assert.Equal("StructureGen", dialog.Title);
            Assert.Contains("Nothing was drawn", dialog.Message);
            Assert.Contains("no geometry generator is registered", dialog.Message);
        }

        [Fact]
        public void A_run_that_draws_nothing_and_explains_nothing_still_says_so()
        {
            // No reason recorded is a defect in StructureGen rather than in the drawing, and the
            // user is told that rather than being left with a palette that appears inert.
            FakeCadService cad = Cad(EmptyRun(new ValidationReport()));
            MainViewModel palette = Palette(cad);

            palette.GenerateAllCommand.Execute(null);

            (string Title, string Message) dialog = Assert.Single(cad.Dialogs);
            Assert.Contains("Nothing was drawn", dialog.Message);
            Assert.Contains("No reason was reported", dialog.Message);
        }

        [Fact]
        public void A_run_that_draws_something_does_not_interrupt_with_a_dialog()
        {
            FakeCadService cad = Cad(_ => Result<GenerationSummary>.Success(new GenerationSummary(
                new Dictionary<Guid, IReadOnlyList<string>> { [Guid.NewGuid()] = new[] { "2AF" } },
                1,
                0,
                new ValidationReport())));

            MainViewModel palette = Palette(cad);

            palette.GenerateAllCommand.Execute(null);

            Assert.Empty(cad.Dialogs);
            Assert.Contains(cad.Messages, message => message.Contains("created 1 object"));
        }

        [Fact]
        public void A_warning_alongside_real_geometry_is_reported_without_a_dialog()
        {
            ValidationReport report = new();
            report.AddWarning("Cad.UnitlessDrawing", "This drawing does not declare its insertion units.");

            FakeCadService cad = Cad(_ => Result<GenerationSummary>.Success(new GenerationSummary(
                new Dictionary<Guid, IReadOnlyList<string>> { [Guid.NewGuid()] = new[] { "2AF" } },
                1,
                0,
                report)));

            MainViewModel palette = Palette(cad);

            palette.GenerateAllCommand.Execute(null);

            Assert.Empty(cad.Dialogs);
            Assert.Contains(cad.Messages, message => message.Contains("does not declare its insertion units"));
        }

        [Fact]
        public void An_exception_under_a_palette_button_reaches_the_user_instead_of_vanishing()
        {
            // The failure this guards against is specific to where the palette runs. A command
            // method has a catch-all around it; a button on a modeless palette runs on the WPF
            // dispatcher, which inside AutoCAD's palette host swallows what escapes - while the
            // transaction underneath rolls the operation back on disposal. Nothing drawn, nothing
            // said.
            List<Exception> caught = new();
            RelayCommand.UnhandledException = caught.Add;

            try
            {
                FakeCadService cad = Cad(_ => throw new InvalidOperationException("the drawing is read-only"));
                MainViewModel palette = Palette(cad);

                palette.GenerateAllCommand.Execute(null);

                Exception reported = Assert.Single(caught);
                Assert.Equal("the drawing is read-only", reported.Message);
            }
            finally
            {
                RelayCommand.UnhandledException = null;
            }
        }

        [Fact]
        public void With_no_handler_installed_an_exception_is_left_exactly_as_it_was()
        {
            // The net must not become a way for a defect to go unnoticed anywhere else. With no
            // handler installed the exception filter does not run, so nothing is caught and the
            // original stack survives for a debugger or a test to see.
            RelayCommand.UnhandledException = null;

            FakeCadService cad = Cad(_ => throw new InvalidOperationException("boom"));
            MainViewModel palette = Palette(cad);

            InvalidOperationException thrown =
                Assert.Throws<InvalidOperationException>(() => palette.GenerateAllCommand.Execute(null));

            Assert.Equal("boom", thrown.Message);
        }

        [Fact]
        public void The_plan_buttons_ask_for_the_layout_they_name_and_report_a_refusal()
        {
            FakeCadService cad = Cad(EmptyRun(new ValidationReport()));
            MainViewModel palette = Palette(cad);

            palette.Bom.DrawArrangementPlanCommand.Execute(null);
            palette.Bom.DrawItemPlansCommand.Execute(null);

            Assert.Equal(
                new[] { PlanLayout.Arrangement, PlanLayout.Items },
                cad.PlanRequests.Select(r => r.Options.Layout));
            Assert.All(cad.PlanRequests, r => Assert.Equal(HarvestScope.ModelSpace, r.Scope));
            Assert.Contains("no drawing to draw a plan in", palette.Status);
        }

        private static Func<GenerationRequest, Result<GenerationSummary>> EmptyRun(ValidationReport report) =>
            _ => Result<GenerationSummary>.Success(
                new GenerationSummary(new Dictionary<Guid, IReadOnlyList<string>>(), 0, 0, report),
                report);

        private static FakeCadService Cad(Func<GenerationRequest, Result<GenerationSummary>> generate) =>
            new(generate);

        private static MainViewModel Palette(FakeCadService cad)
        {
            StructureGenContext context = StructureGenContext.Create(
                NullLogger.Instance, new FixedSettingsStore(TestData.DemoDatabasePath));

            MainViewModel palette = new(context, cad);

            // One valid item, so that the run is attempted rather than refused for an empty list.
            SectionRecord record = context.Database.Current.Sections.First(s => s.Designation == "IPE 300");
            palette.List.Add(TestData.FromRecord(record));

            return palette;
        }

        /// <summary>A settings store that hands out fixed settings and persists nothing.</summary>
        private sealed class FixedSettingsStore : ISettingsStore
        {
            private readonly string _databasePath;

            public FixedSettingsStore(string databasePath) => _databasePath = databasePath;

            public string SettingsFilePath => "(none)";

            public ProjectSettings Load() => new() { DatabasePath = _databasePath };

            public Result Save(ProjectSettings settings) => Result.Success();
        }
    }
}
