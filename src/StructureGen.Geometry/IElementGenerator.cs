using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry
{
    /// <summary>
    /// Everything the application needs to know about one family of structural elements.
    /// </summary>
    /// <remarks>
    /// A generator is deliberately free of any AutoCAD type. It answers four questions — what may
    /// the user edit, is this definition sound, what shape is it, and what does it weigh — and the
    /// AutoCAD layer turns the answer to the third into entities. Adding a new element type
    /// therefore means writing one class against this interface and registering it; nothing in the
    /// user interface, the bill of materials, the exporter or the AutoCAD layer needs to change.
    /// See <c>docs/ADDING-ELEMENT-TYPES.md</c> for a worked example.
    /// </remarks>
    public interface IElementGenerator
    {
        /// <summary>Gets the category identifier this generator serves.</summary>
        string CategoryId { get; }

        /// <summary>Gets the dimension keys this generator cannot produce geometry without.</summary>
        IReadOnlyList<string> RequiredDimensions { get; }

        /// <summary>
        /// Describes which parameters the user may edit for a given record, and which are fixed by
        /// the standard.
        /// </summary>
        /// <param name="record">The database record the element is based on.</param>
        /// <returns>The schema.</returns>
        ParameterSchema GetParameterSchema(SectionRecord record);

        /// <summary>Checks that a definition can be turned into geometry.</summary>
        /// <param name="definition">The definition to check.</param>
        /// <param name="record">
        /// The database record the element claims to come from, when it is still available. Supplying
        /// it enables the check that no standardised dimension has been altered outside custom mode.
        /// </param>
        /// <returns>A report. An empty report means the definition is sound.</returns>
        ValidationReport Validate(ElementDefinition definition, SectionRecord? record = null);

        /// <summary>Builds the cross-sections and extrusion lengths for one piece.</summary>
        /// <param name="definition">The definition to realise.</param>
        /// <returns>The geometry, or a failure carrying a user-facing reason.</returns>
        Result<ElementGeometry> BuildGeometry(ElementDefinition definition);

        /// <summary>Computes the mass of the definition's whole quantity, and records how.</summary>
        /// <param name="definition">The definition to weigh.</param>
        /// <param name="geometry">
        /// The generated geometry, used when the mass has to be derived from volume and density.
        /// May be <see langword="null"/> when only a tabulated mass is needed.
        /// </param>
        /// <returns>The mass figures together with a traceable explanation.</returns>
        MassProperties ComputeMass(ElementDefinition definition, ElementGeometry? geometry);
    }
}
