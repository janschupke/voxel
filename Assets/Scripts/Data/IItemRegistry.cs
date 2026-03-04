using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Abstraction for item definitions. Domain code depends on this, not ScriptableObject.
    /// </summary>
    public interface IItemRegistry
    {
        ItemDefinition GetDefinition(Item item);
    }
}
