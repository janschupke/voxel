using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "IslandStageConfig", menuName = "Voxel/Island Stage Config")]
    public class IslandStageConfig : ScriptableObject
    {
        [SerializeField] [Min(1)] [Tooltip("Minimum island radius in voxels")]
        private int minIslandRadius = 5;

        [SerializeField] [Min(1)] [Tooltip("Maximum island radius in voxels")]
        private int maxIslandRadius = 20;

        [SerializeField] [Min(0)] [Tooltip("Minimum water gap in voxels between island edges (ensures islands never touch)")]
        private int minGapBetweenIslands = 5;

        [SerializeField] [Range(0f, 1f)] [Tooltip("How much noise perturbs island boundaries (0 = perfect circles, 1 = very irregular)")]
        private float shapeIrregularity = 0.35f;

        [SerializeField] [Min(100)] [Tooltip("World area divided by this gives approximate island count")]
        private int islandDensity = 5000;

        [SerializeField] [Range(0f, 0.5f)] [Tooltip("Fraction of width/depth to keep empty at corners (0-0.2 typical)")]
        private float cornerMarginPercent = 0.1f;

        [SerializeField] [Min(1)] [Tooltip("Base height of islands above sea bed (must be > waterLevelY for dry land)")]
        private int baseIslandHeight = 18;

        public int MinIslandRadius => minIslandRadius;
        public int MaxIslandRadius => maxIslandRadius;
        public int MinGapBetweenIslands => minGapBetweenIslands;
        public float ShapeIrregularity => shapeIrregularity;
        public int IslandDensity => islandDensity;
        public float CornerMarginPercent => cornerMarginPercent;
        public int BaseIslandHeight => baseIslandHeight;
    }
}
