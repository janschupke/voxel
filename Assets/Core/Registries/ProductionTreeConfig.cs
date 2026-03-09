using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ProductionTree", menuName = "Voxel/Production/Production Tree")]
    public class ProductionTreeConfig : ScriptableObject
    {
        public RecipeConfig[] Recipes = System.Array.Empty<RecipeConfig>();
    }
}
