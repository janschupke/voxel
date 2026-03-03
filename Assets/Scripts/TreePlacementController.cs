using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Core;

namespace Voxel
{
    public class TreePlacementController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private UIDocument uiDocument;

        private bool _placementModeActive;
        private (int x, int z)? _dragStartBlock;
        private Button _treeButton;

        private VoxelGrid Grid => worldBootstrap?.Grid;
        private Transform TreesParent => worldBootstrap?.TreesParent;
        private GameObject TreePrefab => worldBootstrap?.TreePrefab;
        private WaterConfig WaterConfig => worldBootstrap?.WaterConfig;
        private WorldParameters WorldParameters => worldBootstrap?.WorldParameters;
        private IslandPipelineConfig IslandPipelineConfig => worldBootstrap?.IslandPipelineConfig;

        private WorldScale WorldScale => new WorldScale(WorldParameters != null ? WorldParameters.BlockScale : 1f);

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                _treeButton = uiDocument.rootVisualElement.Q<Button>("Tree");
                if (_treeButton != null && TreePrefab == null)
                {
                    _treeButton.SetEnabled(false);
                    UnityEngine.Debug.LogWarning("[TreePlacement] Tree prefab not available - Tree button disabled.");
                }
            }
        }

        private void Update()
        {
            if (!_placementModeActive || worldBootstrap == null) return;

            if (Mouse.current != null && (Mouse.current.rightButton.wasPressedThisFrame || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)))
            {
                CancelPlacementMode();
                return;
            }

            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _dragStartBlock = GetBlockUnderMouse();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (_dragStartBlock.HasValue)
                {
                    var endBlock = GetBlockUnderMouse();
                    if (endBlock.HasValue)
                        PlaceTreesInArea(_dragStartBlock.Value, endBlock.Value);
                    _dragStartBlock = null;
                }
            }
        }

        public void TogglePlacementMode()
        {
            var houseController = FindAnyObjectByType<HousePlacementController>();
            if (houseController != null)
                houseController.CancelPlacementMode();

            _placementModeActive = !_placementModeActive;
            if (_treeButton != null)
            {
                if (_placementModeActive)
                    _treeButton.AddToClassList("placing");
                else
                    _treeButton.RemoveFromClassList("placing");
            }
            if (!_placementModeActive)
                CancelPlacementMode();
        }

        public void CancelPlacementMode()
        {
            _placementModeActive = false;
            _dragStartBlock = null;
            if (_treeButton != null)
                _treeButton.RemoveFromClassList("placing");
        }

        private (int x, int z)? GetBlockUnderMouse()
        {
            if (Grid == null || WaterConfig == null) return null;

            var cam = Camera.main;
            if (cam == null) return null;

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            if (TryRaycastTopSurface(cam, Grid, WorldScale, out var block))
                return (block.bx, block.bz);
            return null;
        }

        private void PlaceTreesInArea((int x, int z) start, (int x, int z) end)
        {
            if (TreesParent == null || TreePrefab == null) return;

            worldBootstrap.EnsureTreesParent();

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            var treeConfig = IslandPipelineConfig?.TreeScatterConfig;
            float prefabHeight = treeConfig != null ? treeConfig.PrefabHeightInUnits : 2f;
            float scaleMult = treeConfig != null ? treeConfig.ScaleMultiplier : 1f;
            bool randomRotation = treeConfig != null && treeConfig.RandomRotation;
            var scale = WorldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

            int placed = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (x < 0 || x >= Grid.Width || z < 0 || z >= Grid.Depth) continue;

                    int topY = GetTopSolidY(Grid, x, z, Grid.Height);
                    if (topY < 0 || topY < waterLevelY) continue;

                    int surfaceY = topY + 1;
                    if (worldBootstrap.HasHouseAtBlock(x, surfaceY, z)) continue;
                    if (worldBootstrap.HasTreeAtBlock(x, surfaceY, z)) continue;

                    var pos = WorldScale.BlockToWorld(x + 0.5f, surfaceY, z + 0.5f);
                    var rotation = randomRotation
                        ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                        : Quaternion.identity;
                    var instance = Instantiate(TreePrefab, pos, rotation, TreesParent);
                    instance.name = TreePrefab.name;
                    instance.transform.localScale = scale;
                    placed++;
                }
            }

            if (placed > 0)
                worldBootstrap.SaveWorld();
        }

        private static bool TryRaycastTopSurface(Camera cam, VoxelGrid grid, WorldScale scale, out (int bx, int by, int bz) block)
        {
            block = default;

            if (Mouse.current == null) return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            float blockScale = scale.BlockScale;
            if (blockScale <= 0f) return false;

            for (int surfaceY = grid.Height; surfaceY >= 1; surfaceY--)
            {
                float planeY = surfaceY * blockScale;
                float dy = ray.direction.y;
                if (Mathf.Abs(dy) < 0.0001f) continue;

                float t = (planeY - ray.origin.y) / dy;
                if (t <= 0f) continue;

                Vector3 hitWorld = ray.origin + ray.direction * t;
                var (bx, _, bz) = scale.WorldToBlock(hitWorld);

                if (bx < 0 || bx >= grid.Width || bz < 0 || bz >= grid.Depth)
                    continue;

                int topY = GetTopSolidY(grid, bx, bz, grid.Height);
                if (topY < 0 || topY + 1 != surfaceY)
                    continue;

                block = (bx, topY + 1, bz);
                return true;
            }

            return false;
        }

        private static int GetTopSolidY(VoxelGrid grid, int x, int z, int gridHeight)
        {
            for (int y = gridHeight - 1; y >= 0; y--)
            {
                if (grid.IsSolid(x, y, z))
                    return y;
            }
            return -1;
        }

        private void OnDisable()
        {
            if (_placementModeActive)
                CancelPlacementMode();
        }
    }
}
