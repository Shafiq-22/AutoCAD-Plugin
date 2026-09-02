using System;
using System.Globalization;
using System.Text;
using StructureGen.Core.Model;

namespace StructureGen.Services
{
    /// <summary>
    /// Computes the deterministic block definition name that a given element geometry belongs to.
    /// </summary>
    /// <remarks>
    /// This is the mechanism behind the plugin's performance story. Two elements that would produce
    /// identical geometry produce the same name, so they share one block definition and cost the
    /// drawing one lightweight reference each. A thousand identical bolts become one definition and
    /// a thousand references rather than a thousand solids, which is the difference between a
    /// drawing that opens and one that does not.
    ///
    /// Everything that changes the shape is in the name, and nothing that does not. Position,
    /// rotation, quantity, layer and notes are properties of an occurrence and are deliberately
    /// absent, so moving a beam never creates a second definition. The dimensions are folded in as a
    /// hash so that a member edited in custom mode gets its own definition instead of silently
    /// changing the shape of every other member that shares its designation.
    /// </remarks>
    public static class BlockNaming
    {
        /// <summary>The prefix every StructureGen block definition name carries.</summary>
        public const string Prefix = "SG_";

        /// <summary>The greatest length AutoCAD accepts for a symbol table name.</summary>
        private const int MaximumNameLength = 255;

        /// <summary>Computes the block definition name for an element.</summary>
        /// <param name="definition">The element.</param>
        /// <param name="representation">
        /// Which representation the block holds. A solid and a two-dimensional profile of the same
        /// member are different geometry and therefore different definitions.
        /// </param>
        /// <returns>A name that is valid as an AutoCAD symbol table entry.</returns>
        public static string BlockNameFor(ElementDefinition definition, RepresentationMode representation)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            string length = definition.LengthMm is { } value
                ? "L" + value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', 'p')
                : "L0";

            string shape = ShapeHash(definition);

            string name = string.Join("_",
                Prefix.TrimEnd('_'),
                Sanitise(definition.CategoryId),
                Sanitise(definition.Designation),
                length,
                Abbreviate(definition.DetailLevel),
                Abbreviate(representation),
                shape);

            return name.Length <= MaximumNameLength ? name : name.Substring(0, MaximumNameLength);
        }

        /// <summary>
        /// Produces a short, stable hash of everything about an element that affects its shape.
        /// </summary>
        /// <param name="definition">The element.</param>
        /// <returns>Eight hexadecimal characters.</returns>
        /// <remarks>
        /// The framework's own string hash is deliberately randomised per process, so it cannot be
        /// used: a block name has to come out the same tomorrow as it did today, or reopening a
        /// drawing would create duplicate definitions. FNV-1a is small, stable and entirely
        /// sufficient for distinguishing a few thousand shapes.
        /// </remarks>
        public static string ShapeHash(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            StringBuilder builder = new();
            builder.Append(definition.CategoryId).Append('|')
                   .Append(definition.StandardId).Append('|')
                   .Append(definition.SectionRecordId).Append('|')
                   .Append(definition.IsCustom ? "custom" : "standard").Append('|');

            // The dimensions are ordered so that two elements carrying the same values in a
            // different insertion order hash the same.
            string[] parts = new string[definition.Dimensions.Count];
            int index = 0;
            foreach (System.Collections.Generic.KeyValuePair<string, decimal> pair in definition.Dimensions)
            {
                parts[index++] = pair.Key + "=" + pair.Value.ToString("0.######", CultureInfo.InvariantCulture);
            }

            Array.Sort(parts, StringComparer.Ordinal);
            builder.Append(string.Join(";", parts));

            return Fnv1a(builder.ToString()).ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>Reduces text to characters AutoCAD accepts in a symbol table name.</summary>
        /// <param name="text">The text to sanitise.</param>
        /// <returns>The sanitised text, never empty.</returns>
        /// <remarks>
        /// AutoCAD rejects a symbol name containing any of <c>&lt; &gt; / \ " : ; ? * | , = `</c>, and
        /// designations routinely contain some of them. Each is replaced rather than removed so that
        /// two different designations cannot collapse onto the same name.
        /// </remarks>
        public static string Sanitise(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "x";
            }

            StringBuilder builder = new(text!.Length);

            foreach (char character in text)
            {
                bool invalid = character is '<' or '>' or '/' or '\\' or '"' or ':' or ';' or '?' or '*'
                                          or '|' or ',' or '=' or '`' or '\'';

                builder.Append(invalid || char.IsControl(character) ? '-' : character);
            }

            string result = builder.ToString().Trim().Replace(' ', '-');
            return result.Length == 0 ? "x" : result;
        }

        private static string Abbreviate(DetailLevel level) => level switch
        {
            DetailLevel.Simplified => "s",
            DetailLevel.Detailed => "d",
            _ => "n"
        };

        private static string Abbreviate(RepresentationMode mode) => mode switch
        {
            RepresentationMode.Profile2D => "2d",
            RepresentationMode.Both => "both",
            _ => "3d"
        };

        private static uint Fnv1a(string text)
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            foreach (char character in text)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }
    }
}
