using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace StructureGen.Tests
{
    /// <summary>
    /// Checks that the autoloader manifest lists every command the plugin defines.
    /// </summary>
    /// <remarks>
    /// The bundle is registered with <c>LoadOnCommandInvocation</c>, which means AutoCAD does not
    /// load the plugin until the user types one of the commands the manifest names. A command that
    /// exists in the source but is missing from the manifest therefore appears simply not to exist:
    /// typing it in a fresh session gets "Unknown command", with nothing anywhere to explain why.
    /// Because the AutoCAD layer cannot be compiled on a machine without AutoCAD, the two are
    /// compared as text, which is enough to catch the mistake that actually happens - adding a
    /// command and forgetting the manifest.
    /// </remarks>
    public class AutoloaderManifestTests
    {
        private static string BundleFile(string name) =>
            Path.Combine(AppContext.BaseDirectory, "bundle", name);

        [Fact]
        public void Every_command_defined_in_source_is_listed_in_the_manifest()
        {
            IReadOnlyList<string> defined = DefinedCommands();
            Assert.True(defined.Count >= 9, $"Only {defined.Count} commands were found; the extraction is probably broken.");

            XDocument manifest = XDocument.Load(BundleFile("PackageContents.xml"));

            // One <Components> block per AutoCAD release, each with its own command list, so the
            // check has to hold for every one of them rather than for the union.
            List<XElement> blocks = manifest.Descendants()
                .Where(e => e.Name.LocalName == "Commands")
                .ToList();

            Assert.True(blocks.Count >= 3, $"Expected one command list per supported release, found {blocks.Count}.");

            foreach (XElement block in blocks)
            {
                HashSet<string> listed = block.Elements()
                    .Where(e => e.Name.LocalName == "Command")
                    .Select(e => (string?)e.Attribute("Global") ?? string.Empty)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (string command in defined)
                {
                    Assert.True(
                        listed.Contains(command),
                        $"The command '{command}' is defined in the source but is missing from a command list in " +
                        "PackageContents.xml, so it would not load the plugin when typed.");
                }
            }
        }

        [Fact]
        public void Every_command_listed_in_the_manifest_is_defined_in_source()
        {
            HashSet<string> defined = DefinedCommands().ToHashSet(StringComparer.OrdinalIgnoreCase);
            XDocument manifest = XDocument.Load(BundleFile("PackageContents.xml"));

            IEnumerable<string> listed = manifest.Descendants()
                .Where(e => e.Name.LocalName == "Command")
                .Select(e => (string?)e.Attribute("Global") ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string command in listed)
            {
                Assert.True(
                    defined.Contains(command),
                    $"PackageContents.xml lists the command '{command}', which no longer exists in the source. " +
                    "Typing it would load the plugin and then fail.");
            }
        }

        [Fact]
        public void A_command_that_shows_generated_geometry_exists()
        {
            // The one command a user reaches for when a generated element cannot be seen. It is
            // named here so that removing it has to be a deliberate decision.
            Assert.Contains("SGSHOW", DefinedCommands());
        }

        private static IReadOnlyList<string> DefinedCommands()
        {
            string source = File.ReadAllText(BundleFile("StructureGenCommands.cs"));

            return Regex.Matches(source, @"\[CommandMethod\(""(?<name>[A-Z0-9]+)""")
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
