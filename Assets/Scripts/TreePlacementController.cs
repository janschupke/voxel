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
        private PlacementPreview _preview;
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

            UpdatePreview();
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
            _preview?.Clear();
        }

        private (int x, int z)? GetBlockUnderMouse()
        {
            if (Grid == null || WaterConfig == null) return null;

            var cam = Camera.main;
            if (cam == null) return null;

            if (PlacementUtility.TryRaycastTopSurface(cam, Grid, WorldScale, out var block))
                return (block.bx, block.bz);
            return null;
        }

        private void UpdatePreview()
        {
            if (Grid == null || TreePrefab == null || WaterConfig == null)
            {
                _preview?.Clear();
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                _preview?.Clear();
                return;
            }

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            bool isTreeBlockValid(int x, int y, int z) =>
                !worldBootstrap.HasHouseAtBlock(x, y, z) && !worldBootstrap.HasTreeAtBlock(x, y, z);

            var treeConfig = IslandPipelineConfig?.TreeScatterConfig;
            float prefabHeight = treeConfig != null ? treeConfig.PrefabHeightInUnits : 2f;
            float scaleMult = treeConfig != null ? treeConfig.ScaleMultiplier : 1f;
            bool randomRotation = treeConfig != null && treeConfig.RandomRotation;

            _preview ??= new PlacementPreview(TreePrefab, WorldScale, prefabHeight, scaleMult);

            if (_dragStartBlock.HasValue)
            {
                var endBlock = GetBlockUnderMouse();
                if (endBlock.HasValue)
                {
                    _preview.SetAreaWithValidity(_dragStartBlock.Value, endBlock.Value, Grid, waterLevelY,
                        isTreeBlockValid, randomRotation);
                }
                else
                {
                    _preview.Clear();
                }
            }
            else
            {
                if (PlacementUtility.TryRaycastTopSurface(cam, Grid, WorldScale, waterLevelY, out var block, out bool valid))
                {
                    bool treeValid = valid && isTreeBlockValid(block.bx, block.by, block.bz);
                    _preview.SetSingle(block, 0f, treeValid);
                }
                else
                {
                    _preview.Clear();
                }
            }
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

                    int topY = PlacementUtility.GetTopSolidY(Grid, x, z, Grid.Height);
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

        private void OnDisable()
        {
            if (_placementModeActive)
                CancelPlacementMode();
        }
    }
}
