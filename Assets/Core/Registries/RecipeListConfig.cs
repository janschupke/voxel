using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "RecipeList", menuName = "Voxel/Production/Recipe List")]
    public class RecipeListConfig : ScriptableObject
    {
        public RecipeConfig[] Recipes = System.Array.Empty<RecipeConfig>();
    }
}
