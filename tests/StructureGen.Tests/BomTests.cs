using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Bom;
using StructureGen.Core.Model;
using StructureGen.Core.Units;
using StructureGen.Services;
using Xunit;

namespace StructureGen.Tests
{
    public class BomTests
    {
        private static readonly MassCalculator Calculator = new(TestData.Registry());
        private static readonly BomBuilder Builder = new();

        private static BomEntry Entry(
            string designation = "IPE 300",
            decimal? lengthMm = 6000m,
            int quantity = 1,
            decimal? unitWeight = 42.2m,
            string categoryId = ElementCategory.ISection,
            string? layer = null,
            bool isCustom = false,
            decimal? density = null,
            string? handle = null)
        {
            ElementDefinition definition = TestData.Element(
                categoryId,
                categoryId == ElementCategory.Plate ? TestData.Plate : TestData.Ipe300,
                lengthMm,
                quantity,
                unitWeight,
                density,
                designation,
                isCustom: isCustom);

            if (layer is not null)
            {
                definition = definition.With(layer: layer);
            }

            return new BomEntry(definition, Calculator.Compute(definition), handle, layer);
        }

        private static BomOptions Options(bool group = true) => new() { GroupIdenticalElements = group };

        [Fact]
        public void Ten_identical_members_become_one_line_carrying_a_quantity()
        {
            IEnumerable<BomEntry> entries = Enumerable.Range(0, 10).Select(_ => Entry());

            BomDocument bom = Builder.Build(entries, Options());

            Assert.Single(bom.Lines);
            Assert.Equal(10, bom.Lines[0].Quantity);

            // 42.2 kg/m x 6 m x 10 pieces.
            Assert.Equal(2532.0m, bom.Lines[0].TotalMassKg);
        }

        [Fact]
        public void Members_of_different_lengths_stay_on_separate_lines()
        {
            BomDocument bom = Builder.Build(
                new[] { Entry(lengthMm: 6000m), Entry(lengthMm: 6001m) }, Options());

            Assert.Equal(2, bom.Lines.Count);
        }

        [Fact]
        public void A_length_tolerance_merges_members_that_are_the_same_to_within_it()
        {
            BomOptions options = Options();
            options.LengthToleranceMm = 5m;

            BomDocument bom = Builder.Build(new[] { Entry(lengthMm: 6000m), Entry(lengthMm: 6001m) }, options);

            Assert.Single(bom.Lines);
            Assert.Equal(2, bom.Lines[0].Quantity);
        }

        [Fact]
        public void Members_of_different_profiles_stay_on_separate_lines()
        {
            BomDocument bom = Builder.Build(
                new[] { Entry(designation: "IPE 300"), Entry(designation: "IPE 400") }, Options());

            Assert.Equal(2, bom.Lines.Count);
        }

        [Fact]
        public void A_non_conforming_member_is_never_hidden_inside_the_count_of_a_conforming_one()
        {
            // Both say "IPE 300", but one has been edited away from the standard. Merging them
            // would put a member that exists in no catalogue onto a line that claims it does.
            BomDocument bom = Builder.Build(new[] { Entry(), Entry(isCustom: true) }, Options());

            Assert.Equal(2, bom.Lines.Count);
            Assert.Equal(1, bom.Totals.NonConformingLines);
        }

        [Fact]
        public void Layers_do_not_split_lines_unless_the_user_asks()
        {
            BomOptions merged = Options();
            Assert.Single(Builder.Build(new[] { Entry(layer: "A"), Entry(layer: "B") }, merged).Lines);

            BomOptions split = Options();
            split.GroupByLayer = true;
            Assert.Equal(2, Builder.Build(new[] { Entry(layer: "A"), Entry(layer: "B") }, split).Lines.Count);
        }

        [Fact]
        public void Grouping_can_be_turned_off_entirely()
        {
            BomDocument bom = Builder.Build(Enumerable.Range(0, 4).Select(_ => Entry()), Options(group: false));

            Assert.Equal(4, bom.Lines.Count);
            Assert.All(bom.Lines, line => Assert.Equal(1, line.Quantity));
        }

        [Fact]
        public void Totals_add_up_across_lines()
        {
            BomDocument bom = Builder.Build(
                new[]
                {
                    Entry(lengthMm: 6000m, quantity: 2),
                    Entry(designation: "IPE 400", lengthMm: 3000m, quantity: 3, unitWeight: 66.3m)
                },
                Options());

            // 42.2 x 6 x 2 = 506.4 and 66.3 x 3 x 3 = 596.7.
            Assert.Equal(2, bom.Totals.LineCount);
            Assert.Equal(5, bom.Totals.TotalPieces);
            Assert.Equal(1103.1m, bom.Totals.TotalMassKg);
            Assert.Equal(1.1031m, bom.Totals.TotalMassTonnes);
            Assert.Equal(21000m, bom.Totals.TotalLengthMm);
            Assert.Equal(21m, bom.Totals.TotalLengthIn(LengthUnit.Metre));
        }

        [Fact]
        public void A_line_with_no_established_weight_is_excluded_from_the_total_and_counted()
        {
            // The critical property: an unknown weight must not be silently treated as zero, or a
            // grand total would understate the steel without saying so.
            BomDocument bom = Builder.Build(
                new[]
                {
                    Entry(unitWeight: 42.2m),
                    Entry(designation: "PL 10", categoryId: ElementCategory.Plate, unitWeight: null, density: null)
                },
                Options());

            Assert.Equal(2, bom.Totals.LineCount);
            Assert.Equal(253.2m, bom.Totals.TotalMassKg);
            Assert.Equal(1, bom.Totals.LinesWithUnknownMass);
            Assert.False(bom.Totals.IsComplete);
        }

        [Fact]
        public void An_excluded_line_produces_a_caveat_that_travels_with_the_schedule()
        {
            BomDocument bom = Builder.Build(
                new[] { Entry(designation: "PL 10", categoryId: ElementCategory.Plate, unitWeight: null) },
                Options());

            Assert.Contains(bom.Caveats(), c => c.Contains("EXCLUDED"));
            Assert.Contains(bom.Report.Messages, m => m.Code == "Bom.UnknownMass");
        }

        [Fact]
        public void A_line_records_the_calculation_behind_its_weight()
        {
            BomDocument bom = Builder.Build(new[] { Entry(quantity: 4) }, Options());

            Assert.Contains("42.2 kg/m", bom.Lines[0].MassExplanation);
            Assert.Equal(WeightSource.UnitWeightTable, bom.Lines[0].WeightSource);
        }

        [Fact]
        public void Lines_are_numbered_from_one_and_ordered_by_category_then_profile_then_length()
        {
            BomDocument bom = Builder.Build(
                new[]
                {
                    Entry(designation: "M16 bolt", categoryId: ElementCategory.Bolt, lengthMm: null, unitWeight: null),
                    Entry(designation: "IPE 400", lengthMm: 3000m),
                    Entry(designation: "IPE 300", lengthMm: 9000m),
                    Entry(designation: "IPE 300", lengthMm: 6000m)
                },
                Options());

            Assert.Equal(new[] { 1, 2, 3, 4 }, bom.Lines.Select(l => l.ItemNumber));
            Assert.Equal("IPE 300", bom.Lines[0].Designation);
            Assert.Equal(6000m, bom.Lines[0].LengthMm);
            Assert.Equal(9000m, bom.Lines[1].LengthMm);
            Assert.Equal("IPE 400", bom.Lines[2].Designation);
            Assert.Equal("M16 bolt", bom.Lines[3].Designation);
        }

        [Fact]
        public void Adding_a_member_does_not_renumber_the_ones_before_it()
        {
            // A stable ordering matters in practice: a schedule reissued after adding one member
            // should not force a reviewer to re-check every item number.
            BomEntry[] first = { Entry(designation: "IPE 300"), Entry(designation: "IPE 400") };
            BomEntry[] second = { Entry(designation: "IPE 300"), Entry(designation: "IPE 400"), Entry(designation: "IPE 500") };

            BomDocument before = Builder.Build(first, Options());
            BomDocument after = Builder.Build(second, Options());

            Assert.Equal("IPE 300", after.Lines[0].Designation);
            Assert.Equal("IPE 400", after.Lines[1].Designation);
            Assert.Equal(before.Lines[0].ItemNumber, after.Lines[0].ItemNumber);
            Assert.Equal(before.Lines[1].ItemNumber, after.Lines[1].ItemNumber);
        }

        [Fact]
        public void Subtotals_are_produced_for_each_category()
        {
            BomDocument bom = Builder.Build(
                new[]
                {
                    Entry(quantity: 2),
                    Entry(designation: "M16 bolt", categoryId: ElementCategory.Bolt, lengthMm: null,
                        unitWeight: null, density: 7850m, quantity: 50)
                },
                Options());

            Assert.Equal(2, bom.Categories.Count);

            BomCategorySummary beams = bom.Categories.Single(c => c.CategoryId == ElementCategory.ISection);
            Assert.Equal(2, beams.Totals.TotalPieces);
            Assert.Equal(506.4m, beams.Totals.TotalMassKg);
        }

        [Fact]
        public void A_line_keeps_the_handles_of_every_object_it_collapsed()
        {
            BomDocument bom = Builder.Build(
                new[] { Entry(handle: "2A1"), Entry(handle: "2A2"), Entry(handle: "2A3") }, Options());

            Assert.Single(bom.Lines);
            Assert.Equal(new[] { "2A1", "2A2", "2A3" }, bom.Lines[0].Handles);
        }

        [Fact]
        public void The_total_length_of_a_line_is_its_length_times_its_quantity()
        {
            BomDocument bom = Builder.Build(
                Enumerable.Range(0, 3).Select(_ => Entry(lengthMm: 6000m)), Options());

            Assert.Equal(18000m, bom.Lines[0].TotalLengthMm);
            Assert.Equal(18m, bom.Lines[0].TotalLengthIn(LengthUnit.Metre));
        }

        [Fact]
        public void An_empty_schedule_is_empty_rather_than_broken()
        {
            BomDocument bom = Builder.Build(Array.Empty<BomEntry>(), Options());

            Assert.True(bom.IsEmpty);
            Assert.Equal(0, bom.Totals.TotalPieces);
            Assert.Equal(0m, bom.Totals.TotalMassKg);
            Assert.Empty(bom.Caveats());
        }

        [Fact]
        public void Unverified_source_data_is_reported_on_the_schedule()
        {
            ElementDefinition unverified = new(
                Guid.NewGuid(), ElementCategory.ISection, "s", "r", "IPE 300", TestData.Ipe300,
                1, 6000m, unitWeightKgPerM: 42.2m, sourceDataVerified: false);

            BomDocument bom = Builder.Build(
                new[] { new BomEntry(unverified, Calculator.Compute(unverified)) }, Options());

            Assert.Equal(1, bom.Totals.UnverifiedSourceLines);
            Assert.Contains(bom.Caveats(), c => c.Contains("not marked as verified"));
        }

        [Fact]
        public void The_dimension_summary_reads_the_way_a_detailer_writes_it()
        {
            BomDocument bom = Builder.Build(new[] { Entry() }, Options());

            // Depth, width, web, flange - in that order, as on a section table.
            Assert.StartsWith("300 x 150 x 7.1 x 10.7", bom.Lines[0].DimensionSummary);
        }

        [Fact]
        public void The_schedule_records_which_database_it_was_built_against()
        {
            BomDocument bom = Builder.Build(new[] { Entry() }, Options(), "My sections v3.2", "Model space");

            Assert.Equal("My sections v3.2", bom.DatabaseStamp);
            Assert.Equal("Model space", bom.SourceDescription);
        }
    }
}
