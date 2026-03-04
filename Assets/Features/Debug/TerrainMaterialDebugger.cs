using UnityEngine;
using Voxel.Pure;
using Voxel.Rendering;

namespace Voxel.Debug
{
    /// <summary>
    /// Attach to the same GameObject as VoxelGridRenderer to log terrain material diagnostics.
    /// Enable in Inspector to run diagnostics on next Initialize.
    /// </summary>
    public class TerrainMaterialDebugger : MonoBehaviour, ITerrainDebugger
    {
        [SerializeField] private bool enableLogging = true;

        public void OnTerrainInitialized(VoxelGrid grid, ChunkManager chunkManager, TerrainMaterialConfig config)
        {
            if (!enableLogging || grid == null) return;

            UnityEngine.Debug.Log($"[TerrainMaterialDebug] === DIAGNOSTICS ===");
            UnityEngine.Debug.Log($"[TerrainMaterialDebug] Grid: {grid.Width}x{grid.Depth}x{grid.Height}");

            if (config != null)
            {
                var bands = config.Bands;
                UnityEngine.Debug.Log($"[TerrainMaterialDebug] Config bands: {bands?.Count ?? 0}");
                if (bands != null)
                {
                    for (int i = 0; i < bands.Count; i++)
                    {
                        var band = bands[i];
                        var matName = band.material != null ? band.material.name : "null (fallback)";
                        var color = band.material != null ? band.material.color : Color.gray;
                        UnityEngine.Debug.Log($"[TerrainMaterialDebug]   Band {i}: threshold={band.heightThreshold:F2} -> {matName} (color: {color})");
                    }
                }

                // Sample heights
                int sampleX = grid.Width / 2;
                int sampleZ = grid.Depth / 2;
                UnityEngine.Debug.Log($"[TerrainMaterialDebug] Sample column at ({sampleX}, {sampleZ}):");
                for (int y = 0; y < grid.Height; y += grid.Height / 4)
                {
                    float normY = Mathf.Clamp01((y + 0.5f) / grid.Height);
                    int bandIdx = config.GetMaterialIndex(normY);
                    var band = bands[bandIdx];
                    var matName = band.material != null ? band.material.name : "fallback";
                    UnityEngine.Debug.Log($"[TerrainMaterialDebug]   y={y} normY={normY:F3} -> band {bandIdx} ({matName})");
                }
            }
            else
            {
                UnityEngine.Debug.Log($"[TerrainMaterialDebug] No TerrainMaterialConfig - using single material");
            }

            UnityEngine.Debug.Log($"[TerrainMaterialDebug] Chunks: {chunkManager.ChunkCountX}x{chunkManager.ChunkCountY}x{chunkManager.ChunkCountZ}");
            UnityEngine.Debug.Log($"[TerrainMaterialDebug] === END ===");
        }
    }
}
