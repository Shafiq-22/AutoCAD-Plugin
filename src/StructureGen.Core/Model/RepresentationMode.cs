using System;

namespace StructureGen.Core.Model
{
    /// <summary>Which representations to create for an element.</summary>
    [Flags]
    public enum RepresentationMode
    {
        /// <summary>Nothing. Not a valid choice for generation; present so the flags enum is well formed.</summary>
        None = 0,

        /// <summary>A closed two-dimensional profile outline drawn at the insertion point.</summary>
        Profile2D = 1,

        /// <summary>A three-dimensional solid.</summary>
        Solid3D = 2,

        /// <summary>Both the two-dimensional outline and the three-dimensional solid.</summary>
        Both = Profile2D | Solid3D
    }
}
