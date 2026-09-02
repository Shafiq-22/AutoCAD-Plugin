using System;
using System.Collections.Generic;
using StructureGen.Core.Model;

namespace StructureGen.Services
{
    /// <summary>A batch of elements to create in the drawing.</summary>
    public sealed class GenerationRequest
    {
        /// <summary>Initialises a new instance of the <see cref="GenerationRequest"/> class.</summary>
        /// <param name="definitions">The elements to create.</param>
        public GenerationRequest(IReadOnlyList<ElementDefinition> definitions) =>
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));

        /// <summary>Gets the elements to create.</summary>
        public IReadOnlyList<ElementDefinition> Definitions { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a layer is created per category when an element
        /// does not name one.
        /// </summary>
        public bool CreateLayers { get; set; } = true;

        /// <summary>Gets or sets the prefix applied to layers StructureGen creates.</summary>
        public string LayerPrefix { get; set; } = "SG-";

        /// <summary>Gets or sets a value indicating whether the view is zoomed to the new geometry afterwards.</summary>
        public bool ZoomToResult { get; set; }

        /// <summary>
        /// Gets or sets the spacing, in millimetres, between successive pieces of the same item when
        /// the user has not picked an individual position for each.
        /// </summary>
        /// <remarks>
        /// Placing ten identical beams on top of one another would be useless, so pieces beyond the
        /// first are laid out along the local X axis at this spacing unless explicit placements were
        /// supplied. A value of zero stacks them, which is what a schedule-only workflow wants.
        /// </remarks>
        public double AutoSpacingMm { get; set; }
    }
}
