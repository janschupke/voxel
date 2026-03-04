using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "RoadConfig", menuName = "Voxel/Road Config")]
    public class RoadConfig : ScriptableObject
    {
        [Tooltip("Material for rendering road overlay on terrain")]
        public Material RoadMaterial;

        [Tooltip("Texture for road appearance (used by material)")]
        public Texture2D RoadTexture;

        [Header("Block-Dependent Tinting")]
        [Tooltip("Tint applied when road is on Ground blocks")]
        public Color TintForGround = new Color(0.8f, 0.6f, 0.4f);

        [Tooltip("Tint applied when road is on Stone blocks")]
        public Color TintForStone = new Color(0.6f, 0.55f, 0.5f);
    }
}
