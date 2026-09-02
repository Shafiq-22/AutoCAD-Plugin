using System;
using StructureGen.Core.Model;

namespace StructureGen.Bom
{
    /// <summary>
    /// One occurrence of an element as it exists in the drawing, before grouping.
    /// </summary>
    /// <remarks>
    /// Entries are harvested from the metadata carried by the generated objects themselves, not
    /// from the palette's working list. That is what makes the schedule describe the drawing: an
    /// object erased by hand disappears from the next bill of materials, and an object copied by
    /// hand appears on it.
    /// </remarks>
    public sealed class BomEntry
    {
        /// <summary>Initialises a new instance of the <see cref="BomEntry"/> class.</summary>
        /// <param name="definition">The element definition recovered from the object's metadata.</param>
        /// <param name="mass">The mass figures computed for this occurrence.</param>
        /// <param name="handle">The AutoCAD handle of the object, when the entry came from a drawing.</param>
        /// <param name="layer">The layer the object sits on.</param>
        /// <param name="spaceName">The block or layout the object sits in.</param>
        public BomEntry(
            ElementDefinition definition,
            MassProperties mass,
            string? handle = null,
            string? layer = null,
            string? spaceName = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Mass = mass ?? throw new ArgumentNullException(nameof(mass));
            Handle = handle;
            Layer = layer ?? definition.Layer;
            SpaceName = spaceName;
        }

        /// <summary>Gets the element definition recovered from the object's metadata.</summary>
        public ElementDefinition Definition { get; }

        /// <summary>Gets the mass figures computed for this occurrence.</summary>
        public MassProperties Mass { get; }

        /// <summary>Gets the AutoCAD handle of the object, when the entry came from a drawing.</summary>
        public string? Handle { get; }

        /// <summary>Gets the layer the object sits on.</summary>
        public string? Layer { get; }

        /// <summary>Gets the block or layout the object sits in.</summary>
        public string? SpaceName { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Definition} ({Handle ?? "no handle"})";
    }
}
