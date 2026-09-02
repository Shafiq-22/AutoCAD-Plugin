using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using StructureGen.Bom;
using StructureGen.UI.ViewModels;
using Xunit;

namespace StructureGen.Tests
{
    /// <summary>
    /// Checks that every data binding in the palette's markup names a property that actually exists.
    /// </summary>
    /// <remarks>
    /// A misspelled binding path in XAML fails silently. The control simply shows nothing, no
    /// exception is raised, and nothing in a normal build or test run notices. On a palette with a
    /// hundred bindings that is a real hazard, and it is the one class of defect in the user
    /// interface that no other check here can catch, since the markup itself cannot be compiled
    /// without Windows.
    ///
    /// The check resolves each path against every type the palette legitimately binds to: the root
    /// view model, the tab view models it exposes, and the item types used inside templates and grid
    /// columns. A path that resolves against none of them is a typo.
    /// </remarks>
    public class PaletteBindingTests
    {
        private static readonly Type[] BindingRoots =
        {
            typeof(MainViewModel),
            typeof(ElementEditorViewModel),
            typeof(ElementListViewModel),
            typeof(BomViewModel),
            typeof(SettingsViewModel),
            typeof(ParameterRowViewModel),
            typeof(ElementListItemViewModel),
            typeof(BomLine)
        };

        private static string MarkupPath => Path.Combine(AppContext.BaseDirectory, "ui", "StructureGenView.xaml");

        [Fact]
        public void The_palette_markup_is_present_and_is_well_formed_xml()
        {
            Assert.True(File.Exists(MarkupPath), $"The palette markup was not copied to '{MarkupPath}'.");

            // Well-formed markup is a precondition for everything else; a stray unescaped ampersand
            // in a tool tip would stop the palette loading at all.
            System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(MarkupPath);
            Assert.NotNull(document.Root);
        }

        [Fact]
        public void Every_binding_path_in_the_palette_names_a_property_that_exists()
        {
            IReadOnlyList<string> paths = ExtractBindingPaths(File.ReadAllText(MarkupPath));

            Assert.True(paths.Count > 50, $"Only {paths.Count} binding paths were found; the extraction is probably broken.");

            List<string> unresolved = paths
                .Where(path => !BindingRoots.Any(root => Resolves(root, path)))
                .ToList();

            Assert.True(
                unresolved.Count == 0,
                "These binding paths do not resolve against any view model the palette binds to:" +
                Environment.NewLine + string.Join(Environment.NewLine, unresolved));
        }

        [Fact]
        public void Every_command_the_palette_binds_to_is_a_command()
        {
            IEnumerable<string> commandPaths = ExtractBindingPaths(File.ReadAllText(MarkupPath))
                .Where(path => path.EndsWith("Command", StringComparison.Ordinal));

            foreach (string path in commandPaths)
            {
                Type? resolved = BindingRoots
                    .Select(root => ResolveType(root, path))
                    .FirstOrDefault(type => type is not null);

                Assert.True(resolved is not null, $"'{path}' does not resolve to anything.");
                Assert.True(
                    typeof(RelayCommand).IsAssignableFrom(resolved),
                    $"'{path}' resolves to {resolved!.Name}, which is not a command.");
            }
        }

        /// <summary>Pulls the path out of every binding expression in the markup.</summary>
        /// <param name="markup">The XAML.</param>
        /// <returns>The distinct paths, in order.</returns>
        private static IReadOnlyList<string> ExtractBindingPaths(string markup)
        {
            List<string> paths = new();

            foreach (Match match in Regex.Matches(markup, @"\{Binding\s+([^},]+)"))
            {
                string expression = match.Groups[1].Value.Trim();

                if (expression.StartsWith("Path=", StringComparison.Ordinal))
                {
                    expression = expression.Substring(5).Trim();
                }

                // A binding carrying only a converter or a source has no path to check.
                if (expression.Length == 0 ||
                    expression.StartsWith("Converter", StringComparison.Ordinal) ||
                    expression.StartsWith("RelativeSource", StringComparison.Ordinal) ||
                    expression.StartsWith("ElementName", StringComparison.Ordinal) ||
                    expression.StartsWith("Source", StringComparison.Ordinal) ||
                    expression.StartsWith("StringFormat", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!paths.Contains(expression, StringComparer.Ordinal))
                {
                    paths.Add(expression);
                }
            }

            return paths;
        }

        private static bool Resolves(Type root, string path) => ResolveType(root, path) is not null;

        private static Type? ResolveType(Type root, string path)
        {
            Type current = root;

            foreach (string step in path.Split('.'))
            {
                PropertyInfo? property = current.GetProperty(
                    step, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                if (property is null)
                {
                    return null;
                }

                current = property.PropertyType;
            }

            return current;
        }
    }
}
