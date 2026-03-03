using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "TreeScatterConfig", menuName = "Voxel/Tree Scatter Config")]
    public class TreeScatterConfig : ScriptableObject
    {
        [SerializeField] [Tooltip("Tree prefab to place")]
        private GameObject treePrefab;

        [SerializeField] [Range(0f, 1f)] [Tooltip("Base density; fraction of eligible cells that may receive trees")]
        private float density = 0.15f;

        [SerializeField] [Min(1)] [Tooltip("Minimum spacing in voxels for dense clusters")]
        private int minTreeDistance = 2;

        [SerializeField] [Min(1)] [Tooltip("Minimum spacing in voxels for sparse areas")]
        private int maxTreeDistance = 8;

        [SerializeField] [Range(0f, 1f)] [Tooltip("Density noise below this = no trees (clear patches)")]
        private float clearThreshold = 0.1f;

        [SerializeField] [Range(0.5f, 2f)] [Tooltip("Multiplier for placement probability in cluster regions")]
        private float clusterDensityBoost = 1.2f;

        [SerializeField] [Tooltip("Frequency for density noise (low = broad forest/clearing regions)")]
        private float densityNoiseFrequency = 0.015f;

        [SerializeField] [Tooltip("Frequency for cluster noise (higher = smaller sub-clusters)")]
        private float clusterNoiseFrequency = 0.06f;

        [SerializeField] [Tooltip("Skip placement on Stone (mountains)")]
        private bool excludeMountains = true;

        [SerializeField] [Tooltip("Apply random Y rotation for variety")]
        private bool randomRotation = true;

        [SerializeField] [Min(0.01f)] [Tooltip("Prefab height in its local units (used to scale to 1 block). Tree prefab = 2.")]
        private float prefabHeightInUnits = 2f;

        [SerializeField] [Min(0.01f)] [Tooltip("Scale multiplier. 1 = 1 block tall, 2 = 2x bigger, 0.5 = half size.")]
        private float scaleMultiplier = 1f;

        public GameObject TreePrefab => treePrefab;
        public float PrefabHeightInUnits => prefabHeightInUnits;
        public float ScaleMultiplier => scaleMultiplier;
        public float Density => density;
        public int MinTreeDistance => minTreeDistance;
        public int MaxTreeDistance => maxTreeDistance;
        public float ClearThreshold => clearThreshold;
        public float ClusterDensityBoost => clusterDensityBoost;
        public float DensityNoiseFrequency => densityNoiseFrequency;
        public float ClusterNoiseFrequency => clusterNoiseFrequency;
        public bool ExcludeMountains => excludeMountains;
        public bool RandomRotation => randomRotation;
    }
}
