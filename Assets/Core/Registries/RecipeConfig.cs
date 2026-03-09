using UnityEngine;

namespace Voxel
{
    [System.Serializable]
    public struct RecipeInputOutput
    {
        public Item Item;
        public int Count;
    }

    [CreateAssetMenu(fileName = "Recipe", menuName = "Voxel/Production/Recipe")]
    public class RecipeConfig : ScriptableObject
    {
        public string Name;
        public RecipeInputOutput[] Inputs = System.Array.Empty<RecipeInputOutput>();
        public RecipeInputOutput[] Outputs = System.Array.Empty<RecipeInputOutput>();
        public float WorkDurationSeconds = 5f;
    }
}
