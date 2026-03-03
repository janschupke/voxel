using System;
using UnityEngine;

namespace Voxel
{
    [Serializable]
    public class PlacedObjectEntry
    {
        [Tooltip("UI label (e.g. House, Tree)")]
        public string Name;

        [Tooltip("Prefab to instantiate when placing")]
        public GameObject Prefab;

        [Tooltip("When placing, remove trees at the target block(s)")]
        public bool CanReplaceTrees;

        [Tooltip("Enable drag-to-place area selection instead of single-click")]
        public bool ArealPlacementEnabled;

        [Min(0.01f)] [Tooltip("Prefab height in its local units (used to scale to 1 block)")]
        public float PrefabHeightInUnits = 2f;

        [Min(0.01f)] [Tooltip("Scale multiplier")]
        public float ScaleMultiplier = 1f;

        [Tooltip("Apply random Y rotation for variety (e.g. for trees)")]
        public bool RandomRotation;
    }
}
