using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Debug;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Controller for placement mode: state, input binding, preview, delegates to PlacementExecutor.
    /// </summary>
    public class ObjectPlacementController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private PlacedObjectRegistry registry;
        [SerializeField] private UIDocument uiDocument;

        private bool _placementModeActive;
        private PlacedObjectEntry _activeEntry;
        private (int x, int y, int z)? _previewBlock;
        private bool _previewValid;
        private (int x, int z)? _dragStartBlock;
        private PlacementPreview _preview;
        private readonly HashSet<Transform> _hiddenEnvironment = new();
        private float _rotationY;
        private readonly Dictionary<string, Button> _buttonsByType = new();
        private PlacementExecutor _executor;
        private PlacementPreviewUpdater _previewUpdater;
        private Camera _cachedCamera;
        private readonly List<Transform> _transformsBuffer = new List<Transform>(8);
        private Func<bool> _escapeHandler;
        private Func<bool> _rotateHandler;

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
            if (uiDocument == null)
                uiDocument = FindAnyObjectByType<UIDocument>();
            _cachedCamera = Camera.main;
            _executor = worldBootstrap != null ? new PlacementExecutor(worldBootstrap) : null;
            _previewUpdater = worldBootstrap != null && _cachedCamera != null ? new PlacementPreviewUpdater(worldBootstrap, _cachedCamera) : null;

            _escapeHandler = TryCancelPlacementMode;
            HotkeyManager.Instance?.Register(Key.Escape, "ESC", "Cancel placement mode", "When placing", _escapeHandler, HotkeyManager.PriorityModeCancel);

            _rotateHandler = TryRotatePreview;
            HotkeyManager.Instance?.Register(Key.R, "R", "Rotate preview 90°", "When placing", _rotateHandler);
        }

        private bool TryRotatePreview()
        {
            if (!_placementModeActive) return false;
            _rotationY = (_rotationY + 90f) % 360f;
            return true;
        }

        private bool TryCancelPlacementMode()
        {
            if (!_placementModeActive) return false;
            CancelPlacementMode();
            return true;
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
            if (entry == null) return;
            if (!entry.IsSurfaceOverlay && entry.Prefab == null)
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
            RestoreHiddenEnvironment();
            _preview?.Release();
            _preview = null;
            _previewBlock = null;
        }

        private void Update()
        {
            if (!_placementModeActive || worldBootstrap == null || _activeEntry == null) return;

            if (UIPanelUtils.IsPointerOverBlockingUI(uiDocument))
            {
                if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
                    _dragStartBlock = null;
                _preview?.Clear();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacementMode();
                return;
            }

            if (_activeEntry.PlacementMode == PlacementMode.Area || _activeEntry.PlacementMode == PlacementMode.Line)
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
                            {
                                if (_executor != null)
                                {
                                    if (_activeEntry.PlacementMode == PlacementMode.Line)
                                        _executor.PlaceInLine(_dragStartBlock.Value, endBlock.Value, _activeEntry);
                                    else
                                        _executor.PlaceInArea(_dragStartBlock.Value, endBlock.Value, _activeEntry);
                                }
                            }
                            _dragStartBlock = null;
                        }
                    }
                }
            }
            else
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (_previewBlock.HasValue && _previewValid && _executor != null)
                        _executor.PlaceSingle(_previewBlock.Value, _activeEntry, _rotationY);
                    else if (_previewBlock.HasValue && !_previewValid)
                        GameDebugLogger.Log($"[Placement] Click blocked: block {_previewBlock.Value} invalid for '{_activeEntry?.Name}' (previewValid=false)");
                    else if (!_previewBlock.HasValue)
                        GameDebugLogger.Log("[Placement] Click blocked: no preview block under cursor");
                    else if (_executor == null)
                        GameDebugLogger.Log("[Placement] Click blocked: executor is null");
                    return;
                }
            }

            UpdatePreview();
        }

        private (int x, int z)? GetBlockUnderMouse()
        {
            if (Grid == null || WaterConfig == null) return null;
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            return PlacementInputUtils.GetBlockUnderMouse(_cachedCamera, Grid, WorldScale);
        }

        private void UpdatePreview()
        {
            if (Grid == null || WaterConfig == null) return;
            if (_activeEntry != null && !_activeEntry.IsSurfaceOverlay && _activeEntry.Prefab == null)
            {
                _preview?.Clear();
                _previewBlock = null;
                return;
            }

            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera == null) { _preview?.Clear(); _previewBlock = null; return; }
            if (_previewUpdater == null) _previewUpdater = new PlacementPreviewUpdater(worldBootstrap, _cachedCamera);

            if (_activeEntry.Prefab == null) { _preview?.Release(); _preview = null; _previewBlock = null; return; }
            float prefabHeight = _activeEntry.PrefabHeightInUnits > 0 ? _activeEntry.PrefabHeightInUnits : 2f;
            float scaleMult = _activeEntry.ScaleMultiplier > 0 ? _activeEntry.ScaleMultiplier : 1f;
            if (_preview == null || _preview.Prefab != _activeEntry.Prefab)
            {
                _preview?.Release();
                _preview = new PlacementPreview(_activeEntry.Prefab, WorldScale, prefabHeight, scaleMult);
            }

            var dragStart = _activeEntry.PlacementMode == PlacementMode.Area || _activeEntry.PlacementMode == PlacementMode.Line
                ? _dragStartBlock
                : null;

            _previewUpdater.Update(_activeEntry, _preview, dragStart, _rotationY, out var newBlock, out var newValid);

            _previewBlock = newBlock;
            _previewValid = newValid;

            RestoreHiddenEnvironment();
            if (_activeEntry.CanReplaceEnvironment && _preview != null)
            {
                if (_activeEntry.PlacementMode == PlacementMode.Area || _activeEntry.PlacementMode == PlacementMode.Line)
                {
                    foreach (var (x, y, z) in _preview.PreviewBlocks)
                    {
                        if (worldBootstrap.HasEnvironmentAtBlock(x, y, z))
                            HideEnvironmentAtBlock((x, y, z));
                    }
                }
                else if (newValid && newBlock.HasValue && worldBootstrap.HasEnvironmentAtBlock(newBlock.Value.x, newBlock.Value.y, newBlock.Value.z))
                {
                    HideEnvironmentAtBlock(newBlock.Value);
                }
            }
        }

        private void HideEnvironmentAtBlock((int x, int y, int z) block)
        {
            if (worldBootstrap == null || registry == null) return;
            _transformsBuffer.Clear();
            worldBootstrap.GetTransformsAtBlock(block.x, block.y, block.z, _transformsBuffer);
            foreach (var child in _transformsBuffer)
            {
                var entryName = worldBootstrap.GetEntryNameForTransform(child);
                var entry = string.IsNullOrEmpty(entryName) ? null : registry.GetByName(entryName);
                if (entry != null && entry.StructureType == Pure.StructureType.Environment)
                {
                    child.gameObject.SetActive(false);
                    _hiddenEnvironment.Add(child);
                }
            }
        }

        private void RestoreHiddenEnvironment()
        {
            foreach (var t in _hiddenEnvironment)
            {
                if (t != null)
                    t.gameObject.SetActive(true);
            }
            _hiddenEnvironment.Clear();
        }

        private void OnDisable()
        {
            if (_placementModeActive)
                CancelPlacementMode();
        }

        private void OnDestroy()
        {
            if (_escapeHandler != null)
                HotkeyManager.Instance?.Unregister(_escapeHandler);
            if (_rotateHandler != null)
                HotkeyManager.Instance?.Unregister(_rotateHandler);
        }
    }
}
