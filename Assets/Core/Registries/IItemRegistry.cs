using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Abstraction for item definitions. Domain code depends on this, not ScriptableObject.
    /// </summary>
    public interface IItemRegistry
    {
        ItemDefinition GetDefinition(Item item);

        /// <summary>Resolves stable string ID to Item. Returns true if found.</summary>
        bool TryGetByStableId(string stableId, out Item item);

        /// <summary>Returns stable string ID for persistence. Never changes when enum is reordered.</summary>
        string GetStableId(Item item);

        /// <summary>Category display order for inventory UI. Categories not listed appear last.</summary>
        IReadOnlyList<string> CategoryDisplayOrder { get; }

        /// <summary>Returns true if item is final (carriers pick ASAP). False = only collectors pick, carriers take when building full.</summary>
        bool IsFinal(Item item);
    }
}
