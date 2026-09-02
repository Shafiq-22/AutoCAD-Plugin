using System;
using StructureGen.Core.Units;
using Xunit;

namespace StructureGen.Tests
{
    public class UnitConversionTests
    {
        [Theory]
        [InlineData(LengthUnit.Millimetre, 1)]
        [InlineData(LengthUnit.Centimetre, 10)]
        [InlineData(LengthUnit.Metre, 1000)]
        public void Metric_length_factors_are_exact(LengthUnit unit, decimal expectedMillimetres) =>
            Assert.Equal(expectedMillimetres, UnitConverter.ToMillimetres(1m, unit));

        [Fact]
        public void An_inch_is_exactly_25_point_4_millimetres() =>
            Assert.Equal(25.4m, UnitConverter.ToMillimetres(1m, LengthUnit.Inch));

        [Fact]
        public void A_foot_is_exactly_304_point_8_millimetres() =>
            Assert.Equal(304.8m, UnitConverter.ToMillimetres(1m, LengthUnit.Foot));

        [Fact]
        public void A_pound_is_exactly_the_defined_number_of_kilograms() =>
            Assert.Equal(0.45359237m, UnitConverter.ToKilograms(1m, MassUnit.Pound));

        [Fact]
        public void A_tonne_is_a_thousand_kilograms() =>
            Assert.Equal(1000m, UnitConverter.ToKilograms(1m, MassUnit.Tonne));

        [Theory]
        [InlineData(LengthUnit.Millimetre)]
        [InlineData(LengthUnit.Centimetre)]
        [InlineData(LengthUnit.Metre)]
        public void Metric_length_round_trips_are_exact(LengthUnit unit)
        {
            // The metric factors are powers of ten, so a decimal round trip is exact rather than
            // merely close. The same round trip in double is not: it loses accuracy immediately.
            const decimal original = 6000.123m;

            decimal there = UnitConverter.FromMillimetres(original, unit);
            decimal back = UnitConverter.ToMillimetres(there, unit);

            Assert.Equal(original, back);
        }

        [Theory]
        [InlineData(LengthUnit.Inch)]
        [InlineData(LengthUnit.Foot)]
        public void Imperial_length_round_trips_stay_far_inside_any_meaningful_tolerance(LengthUnit unit)
        {
            // Dividing by 25.4 produces a repeating decimal, so the return trip cannot be exact in
            // any finite representation. What matters is the size of what is left over: this asserts
            // it stays below a tenth of a yoctometre, which is around eighteen orders of magnitude
            // smaller than the tightest tolerance any steelwork drawing has ever carried.
            const decimal original = 6000.123m;

            decimal there = UnitConverter.FromMillimetres(original, unit);
            decimal back = UnitConverter.ToMillimetres(there, unit);

            Assert.True(
                Math.Abs(back - original) < 0.0000000000000000000001m,
                $"Round tripping through {unit} drifted by {back - original} mm.");
        }

        [Theory]
        [InlineData(MassUnit.Gram)]
        [InlineData(MassUnit.Kilogram)]
        [InlineData(MassUnit.Tonne)]
        public void Metric_mass_round_trips_are_exact(MassUnit unit)
        {
            const decimal original = 1234.567m;

            decimal there = UnitConverter.FromKilograms(original, unit);
            decimal back = UnitConverter.ToKilograms(there, unit);

            Assert.Equal(original, back);
        }

        [Fact]
        public void Pound_round_trips_stay_far_inside_any_meaningful_tolerance()
        {
            const decimal original = 1234.567m;

            decimal there = UnitConverter.FromKilograms(original, MassUnit.Pound);
            decimal back = UnitConverter.ToKilograms(there, MassUnit.Pound);

            Assert.True(
                Math.Abs(back - original) < 0.0000000000000000000001m,
                $"Round tripping through pounds drifted by {back - original} kg.");
        }

        [Fact]
        public void Converting_between_two_arbitrary_units_agrees_with_going_via_millimetres()
        {
            decimal direct = UnitConverter.ConvertLength(12m, LengthUnit.Foot, LengthUnit.Metre);
            decimal viaMillimetres = UnitConverter.FromMillimetres(
                UnitConverter.ToMillimetres(12m, LengthUnit.Foot), LengthUnit.Metre);

            Assert.Equal(viaMillimetres, direct);
            Assert.Equal(3.6576m, direct);
        }

        [Theory]
        [InlineData("mm", LengthUnit.Millimetre)]
        [InlineData("MM", LengthUnit.Millimetre)]
        [InlineData("millimeter", LengthUnit.Millimetre)]
        [InlineData("m", LengthUnit.Metre)]
        [InlineData("metres", LengthUnit.Metre)]
        [InlineData(" in ", LengthUnit.Inch)]
        [InlineData("feet", LengthUnit.Foot)]
        public void Length_unit_tokens_are_understood(string token, LengthUnit expected)
        {
            Assert.True(UnitConverter.TryParseLengthUnit(token, out LengthUnit actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("furlong")]
        [InlineData("")]
        [InlineData(null)]
        public void An_unrecognised_length_token_is_rejected_rather_than_guessed(string? token) =>
            Assert.False(UnitConverter.TryParseLengthUnit(token, out _));

        [Theory]
        [InlineData("kg", MassUnit.Kilogram)]
        [InlineData("t", MassUnit.Tonne)]
        [InlineData("tonnes", MassUnit.Tonne)]
        [InlineData("lb", MassUnit.Pound)]
        [InlineData("g", MassUnit.Gram)]
        public void Mass_unit_tokens_are_understood(string token, MassUnit expected)
        {
            Assert.True(UnitConverter.TryParseMassUnit(token, out MassUnit actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Length_values_carry_their_unit_with_them()
        {
            Length length = Length.FromMetres(6m);

            Assert.Equal(6000m, length.Millimetres);
            Assert.Equal(6m, length.Metres);
            Assert.Equal(600m, length.To(LengthUnit.Centimetre));
            Assert.True(length.IsPositive);
        }

        [Fact]
        public void Lengths_compare_and_combine()
        {
            Length short_ = Length.FromMillimetres(1000m);
            Length long_ = Length.FromMillimetres(2000m);

            Assert.True(short_ < long_);
            Assert.True(long_ > short_);
            Assert.Equal(Length.FromMillimetres(3000m), short_ + long_);
            Assert.Equal(Length.FromMillimetres(1000m), long_ - short_);
            Assert.Equal(Length.FromMillimetres(4000m), long_ * 2m);
        }

        [Fact]
        public void Masses_convert_to_tonnes()
        {
            Mass mass = Mass.FromKilograms(2500m);

            Assert.Equal(2.5m, mass.Tonnes);
            Assert.Equal(2500m, mass.To(MassUnit.Kilogram));
        }
    }
}
