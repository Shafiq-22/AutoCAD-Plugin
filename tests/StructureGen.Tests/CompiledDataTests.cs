using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Data;
using Xunit;

namespace StructureGen.Tests
{
    /// <summary>
    /// Guards the properties of the shipped data that no amount of geometry checking would catch.
    /// </summary>
    /// <remarks>
    /// Most of the shipped records are compiled from upstream tables by
    /// tools/data-sources/compile_sections.py rather than typed by hand. That makes a different
    /// class of mistake possible: not a wrong number, but a wrong claim about the numbers, or a
    /// family quietly vanishing because a source file was renamed upstream and the loop that read
    /// it found nothing. Both would ship without anything else noticing.
    /// </remarks>
    public class CompiledDataTests
    {
        public static TheoryData<string> ShippedDatabases => new()
        {
            TestData.DemoDatabasePath,
            TestData.AiscDatabasePath,
            TestData.ImperialDatabasePath,
        };

        [Theory]
        [MemberData(nameof(ShippedDatabases))]
        public void Nothing_that_ships_claims_to_be_verified(string path)
        {
            // This project has no standing to mark a record verified: it has not compared it with a
            // controlled copy of a standard, and saying otherwise in a file a detailer will take
            // weights from would be a lie with consequences. Verifying is the user's decision.
            StructuralDatabase database = TestData.LoadShipped(path);

            IReadOnlyList<string> claimed = database.Sections
                .Where(section => section.Verified)
                .Select(section => section.Designation)
                .ToList();

            Assert.True(
                claimed.Count == 0,
                $"{claimed.Count} shipped record(s) claim to be verified, starting with '{claimed.FirstOrDefault()}'.");
        }

        [Theory]
        [MemberData(nameof(ShippedDatabases))]
        public void Every_shipped_record_says_where_it_came_from(string path)
        {
            StructuralDatabase database = TestData.LoadShipped(path);

            IReadOnlyList<string> silent = database.Sections
                .Where(section => string.IsNullOrWhiteSpace(section.Provenance))
                .Select(section => section.Designation)
                .ToList();

            Assert.True(
                silent.Count == 0,
                $"{silent.Count} shipped record(s) carry no provenance, starting with '{silent.FirstOrDefault()}'.");
        }

        [Fact]
        public void The_compiled_datasets_still_contain_every_family_they_are_meant_to()
        {
            // A source file renamed upstream, or a family mapping dropped by an edit, would empty a
            // whole family without failing anything else: the file would still load, and what
            // remained would still be correct. These counts are the floor each family was compiled
            // at, so losing one is a failure rather than a smaller list.
            AssertFamilies(TestData.LoadDemoDatabase(), new Dictionary<string, int>
            {
                // From the published tables in the supplied workbook.
                ["IPE"] = 68,
                ["HE"] = 124,
                ["HD"] = 42,
                ["HL"] = 35,
                ["HLZ"] = 4,
                ["HP"] = 31,
                ["UB"] = 180,
                ["UC"] = 46,
                ["UBP"] = 17,

                // Hollow sections, which the workbook does not cover.
                ["CHS"] = 162,
                ["RHS"] = 125,
                ["SHS"] = 123,

                // Hand written, and left alone by the compilation.
                ["UPN"] = 10,
                ["L"] = 10,
                ["PL"] = 9,
            });

            AssertFamilies(TestData.LoadAiscDatabase(), new Dictionary<string, int>
            {
                ["W"] = 289,
                ["M"] = 16,
                ["S"] = 28,
                ["HP"] = 22,
                ["C"] = 32,
                ["MC"] = 40,
                ["L"] = 137,
                ["WT"] = 289,
                ["MT"] = 14,
                ["ST"] = 28,
                ["HSS"] = 714,
                ["Pipe"] = 51,
            });
        }

        [Fact]
        public void Each_shipped_dataset_identifies_itself_distinctly()
        {
            // StructureGen stamps the database version onto every object it draws and prints it
            // from SGABOUT, which is how "which data is this drawing built from?" and "which build
            // is actually installed?" get answered. Two datasets sharing one name and version
            // answers neither, and that cost a full round trip of a support conversation once.
            (string Name, string Version) metric = Identity(TestData.LoadDemoDatabase());
            (string Name, string Version) american = Identity(TestData.LoadAiscDatabase());

            Assert.NotEqual(metric.Name, american.Name);
            Assert.All(
                new[] { metric, american },
                identity =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(identity.Name));
                    Assert.False(string.IsNullOrWhiteSpace(identity.Version));
                });
        }

        [Fact]
        public void A_superseded_source_leaves_nothing_behind()
        {
            // Replacing a source is where a dataset quietly grows a second copy of itself. The
            // European I and H sections came from a package until the published tables arrived,
            // and the two name the same beam differently - an HEA 300 in one is an HE 300 A in the
            // other - so a merge that matched on identity kept both. That shipped: 72 sections
            // present twice, and nothing failed, because every record was individually valid.
            //
            // The compilation now drops everything it previously wrote and rebuilds it, which is
            // what this holds it to. The old namings must be gone entirely, not merely outnumbered.
            StructuralDatabase database = TestData.LoadDemoDatabase();

            IReadOnlyList<string> stale = database.Sections
                .Where(section => IsFamily(section.Designation, "HEA")
                                  || IsFamily(section.Designation, "HEB")
                                  || IsFamily(section.Designation, "HEM"))
                .Select(section => section.Designation)
                .ToList();

            Assert.True(
                stale.Count == 0,
                $"{stale.Count} record(s) survive from the superseded source, starting with " +
                $"'{stale.FirstOrDefault()}'. The published tables carry these as HE 300 A and so on.");
        }

        [Fact]
        public void No_two_records_share_a_designation()
        {
            // Two records under one designation is unresolvable: an element stores the designation
            // it was drawn from, so a drawing could no longer say which of them it meant.
            //
            // Two designations for one section, on the other hand, is not a fault and is not
            // asserted against. The published tables genuinely carry some sections twice under
            // different names - an HD 260 x 172 and an HE 260 M are the same rolled section, with
            // identical dimensions and mass - and a fabricator orders by whichever name their
            // market uses, so both are shipped.
            List<string> repeated = new();

            foreach (StructuralDatabase database in new[] { TestData.LoadDemoDatabase(), TestData.LoadAiscDatabase() })
            {
                repeated.AddRange(database.Sections
                    .GroupBy(section => section.Designation, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => $"{group.Key} appears {group.Count()} times"));
            }

            Assert.True(repeated.Count == 0, string.Join(Environment.NewLine, repeated.Take(10)));
        }

        [Fact]
        public void The_american_dataset_is_written_in_the_units_its_tables_publish()
        {
            // Converting inches to millimetres before storing them would round every dimension in
            // the file and leave nothing to compare against AISC's own tables. The file declares
            // its units instead and StructureGen converts on load, so a W44X408 stays 44.8 inches
            // deep on disk.
            StructuralDatabase database = TestData.LoadAiscDatabase();
            SectionRecord record = database.Sections.Single(s => s.Designation == "W44X408");

            // 44.8 in is 1137.92 mm.
            Assert.Equal(1137.92m, record.Dimension(DimensionKeys.Height));

            // 408 lb/ft is 607.17 kg/m.
            Assert.Equal(607.17m, decimal.Round(record.UnitWeightKgPerM!.Value, 2));
        }

        private static (string Name, string Version) Identity(StructuralDatabase database) =>
            (database.Metadata.Name ?? string.Empty, database.Metadata.Version ?? string.Empty);

        private static void AssertFamilies(StructuralDatabase database, IReadOnlyDictionary<string, int> expected)
        {
            List<string> failures = new();

            foreach (KeyValuePair<string, int> family in expected)
            {
                int found = database.Sections.Count(section => IsFamily(section.Designation, family.Key));

                if (found < family.Value)
                {
                    failures.Add($"{family.Key}: expected at least {family.Value} record(s), found {found}");
                }
            }

            Assert.True(failures.Count == 0, string.Join(System.Environment.NewLine, failures));
        }

        private static bool IsFamily(string designation, string family)
        {
            if (!designation.StartsWith(family, System.StringComparison.Ordinal))
            {
                return false;
            }

            // "W" must not swallow "WT", and "C" must not swallow "CHS", so whatever follows the
            // family letters has to be part of a size rather than more letters.
            string rest = designation.Substring(family.Length);
            return rest.Length > 0 && !char.IsLetter(rest[0]);
        }
    }
}
