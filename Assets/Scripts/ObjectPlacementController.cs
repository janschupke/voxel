using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Core;

namespace Voxel
{
    public class ObjectPlacementController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private PlacedObjectRegistry registry;

        private bool _placementModeActive;
        private PlacedObjectEntry _activeEntry;
        private (int x, int y, int z)? _previewBlock;
        private bool _previewValid;
        private (int x, int z)? _dragStartBlock;
        private PlacementPreview _preview;
        private readonly HashSet<Transform> _hiddenTrees = new();
        private float _rotationY;
        private readonly Dictionary<string, Button> _buttonsByType = new();

        private VoxelGrid Grid => worldBootstrap?.Grid;
        private WaterConfig WaterConfig => worldBootstrap?.WaterConfig;
        private WorldParameters WorldParameters => worldBootstrap?.WorldParameters;

        private WorldScale WorldScale => new WorldScale(WorldParameters != null ? WorldParameters.BlockScale : 1f);

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (registry == null && worldBootstrap != null)
                registry = worldBootstrap.PlacedObjectRegistry;
        }

        public bool IsPlacementModeActive => _placementModeActive;

        public void RegisterButton(string entryName, Button button)
        {
            if (button != null && !string.IsNullOrEmpty(entryName))
                _buttonsByType[entryName] = button;
        }

        public void TogglePlacementMode(string entryName)
        {
            var entry = registry != null ? registry.GetByName(entryName) : null;
            if (entry == null || entry.Prefab == null)
            {
                if (_buttonsByType.TryGetValue(entryName, out var btn) && btn != null)
                    btn.SetEnabled(false);
                return;
            }

            if (_placementModeActive && _activeEntry != null && _activeEntry.Name == entryName)
            {
                CancelPlacementMode();
                return;
            }

            CancelPlacementMode();
            _activeEntry = entry;
            _placementModeActive = true;
            _rotationY = 0f;

            if (_buttonsByType.TryGetValue(entryName, out var button) && button != null)
                button.AddToClassList("placing");
        }

        public void CancelPlacementMode()
        {
            if (_activeEntry != null && _buttonsByType.TryGetValue(_activeEntry.Name, out var btn) && btn != null)
                btn.RemoveFromClassList("placing");

            _placementModeActive = false;
            _activeEntry = null;
            _rotationY = 0f;
            _dragStartBlock = null;
            RestoreHiddenTrees();
            _preview?.Clear();
            _previewBlock = null;
        }

        private void Update()
        {
            if (!_placementModeActive || worldBootstrap == null || _activeEntry == null) return;

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacementMode();
                return;
            }

            if (_activeEntry.ArealPlacementEnabled)
            {
                if (Mouse.current != null)
                {
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                        _dragStartBlock = GetBlockUnderMouse();
                    else if (Mouse.current.leftButton.wasReleasedThisFrame)
                    {
                        if (_dragStartBlock.HasValue)
                        {
                            var endBlock = GetBlockUnderMouse();
                            if (endBlock.HasValue)
                                PlaceInArea(_dragStartBlock.Value, endBlock.Value);
                            _dragStartBlock = null;
                        }
                    }
                }
            }
            else
            {
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    _rotationY = (_rotationY + 90f) % 360f;

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (_previewBlock.HasValue && _previewValid)
                        PlaceSingle(_previewBlock.Value);
                    return;
                }
            }

            UpdatePreview();
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
            if (Grid == null || _activeEntry?.Prefab == null || WaterConfig == null)
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
            bool isBlockValid(int x, int y, int z) =>
                !worldBootstrap.HasBlockingObjectAtBlock(x, y, z);

            float prefabHeight = _activeEntry.PrefabHeightInUnits > 0 ? _activeEntry.PrefabHeightInUnits : 2f;
            float scaleMult = _activeEntry.ScaleMultiplier > 0 ? _activeEntry.ScaleMultiplier : 1f;
            _preview ??= new PlacementPreview(_activeEntry.Prefab, WorldScale, prefabHeight, scaleMult);

            if (_activeEntry.ArealPlacementEnabled)
            {
                if (_dragStartBlock.HasValue)
                {
                    var endBlock = GetBlockUnderMouse();
                    if (endBlock.HasValue)
                    {
                        _preview.SetAreaWithValidity(_dragStartBlock.Value, endBlock.Value, Grid, waterLevelY,
                            isBlockValid, _activeEntry.RandomRotation);
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
                        bool treeValid = valid && isBlockValid(block.bx, block.by, block.bz);
                        _preview.SetSingle(block, 0f, treeValid);
                    }
                    else
                    {
                        _preview.Clear();
                    }
                }
            }
            else
            {
                if (PlacementUtility.TryRaycastTopSurface(cam, Grid, WorldScale, waterLevelY, out var block, out bool valid))
                {
                    bool placeValid = valid && !worldBootstrap.HasBlockingObjectAtBlock(block.bx, block.by, block.bz)
                        && (_activeEntry.CanReplaceTrees || !worldBootstrap.HasEntryAtBlock("Tree", block.bx, block.by, block.bz));

                    if (!_previewBlock.HasValue || _previewBlock.Value != block || _previewValid != placeValid)
                    {
                        _previewBlock = block;
                        _previewValid = placeValid;
                        RestoreHiddenTrees();
                        if (placeValid && _activeEntry.CanReplaceTrees)
                            HideReplaceableAtBlock(block);
                    }

                    _preview.SetSingle(block, _rotationY, placeValid);
                }
                else
                {
                    _previewBlock = null;
                    _previewValid = false;
                    RestoreHiddenTrees();
                    _preview?.Clear();
                }
            }
        }

        private void HideReplaceableAtBlock((int x, int y, int z) block)
        {
            var parent = worldBootstrap?.GetParentByEntryName("Tree");
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
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

        private void PlaceSingle((int x, int y, int z) block)
        {
            var parent = worldBootstrap.GetParentForEntry(_activeEntry);
            if (parent == null || _activeEntry?.Prefab == null) return;

            if (_activeEntry.CanReplaceTrees)
                RemoveTreesAtBlock(block);

            var pos = WorldScale.BlockToWorld(block.x + 0.5f, block.y, block.z + 0.5f);
            var rotation = Quaternion.Euler(0f, _rotationY, 0f);
            float prefabHeight = _activeEntry.PrefabHeightInUnits > 0 ? _activeEntry.PrefabHeightInUnits : 2f;
            float scaleMult = _activeEntry.ScaleMultiplier > 0 ? _activeEntry.ScaleMultiplier : 1f;
            var scale = WorldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

            var instance = Instantiate(_activeEntry.Prefab, pos, rotation, parent);
            instance.name = _activeEntry.Prefab.name;
            instance.transform.localScale = scale;

            worldBootstrap.SaveWorld();
        }

        private void PlaceInArea((int x, int z) start, (int x, int z) end)
        {
            var parent = worldBootstrap.GetParentForEntry(_activeEntry);
            if (parent == null || _activeEntry?.Prefab == null) return;

            worldBootstrap?.GetOrCreateParentForEntry(_activeEntry.Name);

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            float prefabHeight = _activeEntry.PrefabHeightInUnits > 0 ? _activeEntry.PrefabHeightInUnits : 2f;
            float scaleMult = _activeEntry.ScaleMultiplier > 0 ? _activeEntry.ScaleMultiplier : 1f;
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
                    if (worldBootstrap.HasBlockingObjectAtBlock(x, surfaceY, z)) continue;

                    var pos = WorldScale.BlockToWorld(x + 0.5f, surfaceY, z + 0.5f);
                    var rotation = _activeEntry.RandomRotation
                        ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                        : Quaternion.identity;
                    var instance = Instantiate(_activeEntry.Prefab, pos, rotation, parent);
                    instance.name = _activeEntry.Prefab.name;
                    instance.transform.localScale = scale;
                    placed++;
                }
            }

            if (placed > 0)
                worldBootstrap.SaveWorld();
        }

        private void RemoveTreesAtBlock((int x, int y, int z) block)
        {
            var parent = worldBootstrap?.GetParentByEntryName("Tree");
            if (parent == null) return;

            var toDestroy = new List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
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
