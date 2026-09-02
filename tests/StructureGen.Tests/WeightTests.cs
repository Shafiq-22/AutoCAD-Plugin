using System;
using System.Collections.Generic;
using StructureGen.Core.Model;
using StructureGen.Geometry;
using StructureGen.Services;
using Xunit;

namespace StructureGen.Tests
{
    public class WeightTests
    {
        private static readonly MassCalculator Calculator = new(TestData.Registry());

        [Fact]
        public void A_tabulated_mass_per_metre_is_multiplied_by_length_and_quantity()
        {
            // 42.2 kg/m x 6.000 m x 4 = 1012.800 kg, exactly.
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, lengthMm: 6000m, quantity: 4, unitWeight: 42.2m));

            Assert.Equal(WeightSource.UnitWeightTable, mass.Source);
            Assert.Equal(1012.8m, mass.TotalMassKg);
            Assert.Equal(42.2m, mass.UnitWeightKgPerM);
            Assert.True(mass.IsKnown);
        }

        [Fact]
        public void The_calculation_is_written_out_so_a_checker_can_reproduce_it()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, lengthMm: 6000m, quantity: 4, unitWeight: 42.2m));

            Assert.Contains("42.2 kg/m", mass.Explanation);
            Assert.Contains("6 m", mass.Explanation);
            Assert.Contains("1012.8 kg", mass.Explanation);
        }

        [Fact]
        public void A_length_that_is_not_a_whole_number_of_metres_still_totals_exactly()
        {
            // 42.2 x 3.333 x 3 = 421.9578, which rounds to 421.958 rather than drifting.
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, lengthMm: 3333m, quantity: 3, unitWeight: 42.2m));

            Assert.Equal(421.958m, mass.TotalMassKg);
        }

        [Fact]
        public void A_profile_with_no_tabulated_mass_is_weighed_from_its_volume_and_density()
        {
            // A 200 x 10 plate, 2 m long: 200 x 10 x 2000 mm is 0.004 m3, at 7850 kg/m3 that is 31.4 kg.
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.Plate, TestData.Plate, lengthMm: 2000m, quantity: 1, density: 7850m));

            Assert.Equal(WeightSource.ComputedFromDensity, mass.Source);
            Assert.Equal(31.4m, mass.TotalMassKg);
            Assert.Contains("7850 kg/m3", mass.Explanation);
        }

        [Fact]
        public void A_density_derived_weight_also_reports_an_equivalent_mass_per_metre()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.Plate, TestData.Plate, lengthMm: 2000m, quantity: 1, density: 7850m));

            // 31.4 kg over 2 m is 15.7 kg/m.
            Assert.Equal(15.7m, mass.UnitWeightKgPerM);
        }

        [Fact]
        public void A_profile_with_neither_a_mass_nor_a_density_is_reported_as_unknown_and_never_as_zero()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.Plate, TestData.Plate, lengthMm: 2000m, quantity: 5, density: null));

            Assert.Equal(WeightSource.Unavailable, mass.Source);
            Assert.Null(mass.TotalMassKg);
            Assert.False(mass.IsKnown);
            Assert.Contains("rather than estimated", mass.Explanation);
        }

        [Fact]
        public void A_tabulated_mass_takes_precedence_over_a_density()
        {
            // The published figure is authoritative; the geometry is an approximation of it.
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection, TestData.Ipe300,
                lengthMm: 1000m, quantity: 1, unitWeight: 42.2m, density: 7850m));

            Assert.Equal(WeightSource.UnitWeightTable, mass.Source);
            Assert.Equal(42.2m, mass.TotalMassKg);
        }

        [Fact]
        public void A_fastener_is_weighed_per_piece_rather_than_per_metre()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                lengthMm: null, quantity: 250, pieceMass: 0.135m));

            Assert.Equal(WeightSource.PieceMassTable, mass.Source);
            Assert.Equal(33.75m, mass.TotalMassKg);
            Assert.Contains("kg/piece", mass.Explanation);
        }

        [Fact]
        public void A_fastener_with_no_published_piece_mass_falls_back_to_its_modelled_volume()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.Bolt,
                new Dictionary<string, decimal> { ["dth"] = 16m, ["s"] = 24m, ["k"] = 10m, ["l"] = 60m },
                lengthMm: null, quantity: 100, density: 7850m));

            Assert.Equal(WeightSource.ComputedFromDensity, mass.Source);
            Assert.True(mass.TotalMassKg > 0m);

            // A hexagon head on a plain cylinder: roughly a tenth of a kilogram per M16 x 60 bolt.
            Assert.InRange(mass.TotalMassKg!.Value, 8m, 18m);
        }

        [Fact]
        public void A_quantity_of_zero_weighs_nothing_without_failing()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection, TestData.Ipe300, lengthMm: 6000m, quantity: 0, unitWeight: 42.2m));

            Assert.Equal(0m, mass.TotalMassKg);
        }

        [Fact]
        public void Weighing_does_not_build_geometry_when_a_tabulated_mass_exists()
        {
            // A section whose dimensions could not possibly generate still weighs correctly, which
            // proves the fast path is taken. On a schedule of thousands of members this is the
            // difference between an instant answer and a visible pause.
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection,
                new Dictionary<string, decimal> { ["h"] = 300m },
                lengthMm: 6000m, quantity: 1, unitWeight: 42.2m));

            Assert.Equal(WeightSource.UnitWeightTable, mass.Source);
            Assert.Equal(253.2m, mass.TotalMassKg);
        }

        [Fact]
        public void An_element_whose_geometry_cannot_be_built_reports_an_unknown_weight_rather_than_throwing()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                ElementCategory.ISection,
                new Dictionary<string, decimal> { ["h"] = 300m },
                lengthMm: 6000m, quantity: 1, density: 7850m));

            Assert.Equal(WeightSource.Unavailable, mass.Source);
            Assert.Null(mass.TotalMassKg);
        }

        [Fact]
        public void An_unregistered_category_reports_an_unknown_weight()
        {
            MassProperties mass = Calculator.Compute(TestData.Element(
                "not-a-category", TestData.Ipe300, unitWeight: 42.2m));

            Assert.Equal(WeightSource.Unavailable, mass.Source);
            Assert.Contains("not-a-category", mass.Explanation);
        }

        [Fact]
        public void Every_weighable_record_in_the_shipped_database_produces_a_weight()
        {
            Data.StructuralDatabase database = TestData.LoadDemoDatabase();
            MassCalculator calculator = new(TestData.Registry(() => database));
            List<string> failures = new();

            foreach (SectionRecord record in database.Sections)
            {
                MassProperties mass = calculator.Compute(TestData.FromRecord(record));

                if (!mass.IsKnown)
                {
                    failures.Add($"{record.Designation}: {mass.Explanation}");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
