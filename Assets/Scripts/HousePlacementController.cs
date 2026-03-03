using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Core;

namespace Voxel
{
    public class HousePlacementController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private UIDocument uiDocument;

        private bool _placementModeActive;
        private (int x, int y, int z)? _previewBlock;
        private bool _previewValid;
        private PlacementPreview _preview;
        private readonly HashSet<Transform> _hiddenTrees = new();
        private Button _houseButton;
        private float _rotationY;

        private VoxelGrid Grid => worldBootstrap?.Grid;
        private Transform TreesParent => worldBootstrap?.TreesParent;
        private Transform HousesParent => worldBootstrap?.HousesParent;
        private GameObject HousePrefab => worldBootstrap?.HousePrefab;
        private WaterConfig WaterConfig => worldBootstrap?.WaterConfig;
        private WorldParameters WorldParameters => worldBootstrap?.WorldParameters;

        private WorldScale WorldScale => new WorldScale(WorldParameters != null ? WorldParameters.BlockScale : 1f);

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                _houseButton = uiDocument.rootVisualElement.Q<Button>("House");
                if (_houseButton != null && HousePrefab == null)
                {
                    _houseButton.SetEnabled(false);
                    UnityEngine.Debug.LogWarning("[HousePlacement] House prefab not assigned in WorldBootstrap - House button disabled.");
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

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _rotationY = (_rotationY + 90f) % 360f;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_previewBlock.HasValue && _previewValid)
                {
                    PlaceHouse(_previewBlock.Value);
                }
                return;
            }

            UpdatePreview();
        }

        public void TogglePlacementMode()
        {
            var treeController = FindAnyObjectByType<TreePlacementController>();
            if (treeController != null)
                treeController.CancelPlacementMode();

            _placementModeActive = !_placementModeActive;
            if (_houseButton != null)
            {
                if (_placementModeActive)
                    _houseButton.AddToClassList("placing");
                else
                    _houseButton.RemoveFromClassList("placing");
            }
            if (!_placementModeActive)
                CancelPlacementMode();
        }

        public void CancelPlacementMode()
        {
            _placementModeActive = false;
            _rotationY = 0f;
            if (_houseButton != null)
                _houseButton.RemoveFromClassList("placing");
            RestoreHiddenTrees();
            _preview?.Clear();
            _previewBlock = null;
        }

        private void UpdatePreview()
        {
            if (Grid == null || HousePrefab == null || WaterConfig == null)
            {
                _preview?.Clear();
                _previewBlock = null;
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                _preview?.Clear();
                _previewBlock = null;
                return;
            }

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            if (PlacementUtility.TryRaycastTopSurface(cam, Grid, WorldScale, waterLevelY, out var block, out bool valid))
            {
                if (!_previewBlock.HasValue || _previewBlock.Value != block || _previewValid != valid)
                {
                    _previewBlock = block;
                    _previewValid = valid;
                    RestoreHiddenTrees();
                    if (valid)
                        HideTreesAtBlock(block);
                }

                _preview ??= new PlacementPreview(HousePrefab, WorldScale, 2f, 1f);
                _preview.SetSingle(block, _rotationY, valid);
            }
            else
            {
                _previewBlock = null;
                _previewValid = false;
                RestoreHiddenTrees();
                _preview?.Clear();
            }
        }

        private void HideTreesAtBlock((int x, int y, int z) block)
        {
            if (TreesParent == null) return;

            for (int i = 0; i < TreesParent.childCount; i++)
            {
                var child = TreesParent.GetChild(i);
                var (bx, by, bz) = WorldScale.WorldToBlock(child.position);
                if (bx == block.x && by == block.y && bz == block.z)
                {
                    child.gameObject.SetActive(false);
                    _hiddenTrees.Add(child);
                }
            }
        }

        private void RestoreHiddenTrees()
        {
            foreach (var t in _hiddenTrees)
            {
                if (t != null)
                    t.gameObject.SetActive(true);
            }
            _hiddenTrees.Clear();
        }

        private void PlaceHouse((int x, int y, int z) block)
        {
            if (HousesParent == null || HousePrefab == null) return;

            RemoveTreesAtBlock(block);

            var pos = WorldScale.BlockToWorld(block.x + 0.5f, block.y, block.z + 0.5f);
            var rotation = Quaternion.Euler(0f, _rotationY, 0f);
            var instance = Instantiate(HousePrefab, pos, rotation, HousesParent);
            instance.name = HousePrefab.name;
            instance.transform.localScale = WorldScale.ScaleVectorForBlockSizedPrefab(2f);

            worldBootstrap.SaveWorld();
        }

        private void RemoveTreesAtBlock((int x, int y, int z) block)
        {
            if (TreesParent == null) return;

            var toDestroy = new List<Transform>();
            for (int i = 0; i < TreesParent.childCount; i++)
            {
                var child = TreesParent.GetChild(i);
                var (bx, by, bz) = WorldScale.WorldToBlock(child.position);
                if (bx == block.x && by == block.y && bz == block.z)
                    toDestroy.Add(child);
            }
            foreach (var t in toDestroy)
            {
                _hiddenTrees.Remove(t);
                Destroy(t.gameObject);
            }
        }

        private void OnDisable()
        {
            if (_placementModeActive)
                CancelPlacementMode();
        }
    }
}
