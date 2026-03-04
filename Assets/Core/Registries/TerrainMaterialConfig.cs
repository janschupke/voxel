using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    [Serializable]
    public class HeightMaterialBand
    {
        [Range(0f, 1f)]
        [Tooltip("Normalized height (0-1) where this band starts. First band should be 0.")]
        public float heightThreshold;

        public Material material;
    }

    [CreateAssetMenu(fileName = "TerrainMaterialConfig", menuName = "Voxel/Terrain Material Config")]
    public class TerrainMaterialConfig : ScriptableObject
    {
        [SerializeField]
        private List<HeightMaterialBand> bands = new()
        {
            new HeightMaterialBand { heightThreshold = 0f },
            new HeightMaterialBand { heightThreshold = 0.33f },
            new HeightMaterialBand { heightThreshold = 0.66f }
        };

        public IReadOnlyList<HeightMaterialBand> Bands => bands;

        public int GetMaterialIndex(float normalizedHeight)
        {
            if (bands == null || bands.Count == 0)
                return 0;

            int index = 0;
            for (int i = bands.Count - 1; i >= 0; i--)
            {
                if (normalizedHeight >= bands[i].heightThreshold)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        public int BandCount => bands != null ? Math.Max(1, bands.Count) : 1;
    }
}
