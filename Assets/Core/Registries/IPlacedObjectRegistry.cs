using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Abstraction for placed object entries. Domain code depends on this, not ScriptableObject.
    /// </summary>
    public interface IPlacedObjectRegistry
    {
        IReadOnlyList<PlacedObjectEntry> Entries { get; }
        PlacedObjectEntry GetByName(string name);
        PlacedObjectEntry GetGlobalStorageEntry();

        /// <summary>Category display order for building menu. Categories not listed appear last.</summary>
        IReadOnlyList<string> CategoryDisplayOrder { get; }
    }
}