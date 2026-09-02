using StructureGen.Core.Results;

namespace StructureGen.Data
{
    /// <summary>Reads a structural database from persistent storage.</summary>
    public interface IDatabaseLoader
    {
        /// <summary>Loads a database from a file.</summary>
        /// <param name="path">The absolute path of the file to read.</param>
        /// <returns>
        /// The loaded database on success. On failure the result carries a message written for the
        /// end user and, where the file was readable but its content was not usable, a validation
        /// report naming every offending record.
        /// </returns>
        Result<StructuralDatabase> Load(string path);
    }
}
