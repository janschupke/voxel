using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Controller for removal mode: red preview, click/drag to remove structures, roads, and trees.</summary>
    public class RemovalController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private ObjectPlacementController placementController;
        [SerializeField] private SelectionController selectionController;

        private bool _removalModeActive;
        private (int x, int z)? _dragStartBlock;
        private RemovalExecutor _executor;
        private RemovalPreview _preview;
        private Camera _cachedCamera;
        private readonly List<(int x, int y, int z)> _blocksBuffer = new();

        private VoxelGrid Grid => worldBootstrap?.Grid;
        private WaterConfig WaterConfig => worldBootstrap?.WaterConfig;
        private WorldScale WorldScale => new WorldScale(worldBootstrap?.WorldParameters != null ? worldBootstrap.WorldParameters.BlockScale : 1f);

        public bool IsRemovalModeActive => _removalModeActive;

        private void Start()
        {
            if (worldBootstrap == null) worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (uiDocument == null) uiDocument = FindAnyObjectByType<UIDocument>();
            if (placementController == null) placementController = FindAnyObjectByType<ObjectPlacementController>();
            if (selectionController == null) selectionController = FindAnyObjectByType<SelectionController>();
            _cachedCamera = Camera.main;
            _executor = worldBootstrap != null ? new RemovalExecutor(worldBootstrap) : null;
            _preview = worldBootstrap != null ? new RemovalPreview(worldBootstrap) : null;
        }

        public void ToggleRemovalMode()
        {
            if (_removalModeActive)
                CancelRemovalMode();
            else
                EnterRemovalMode();
        }

        public void EnterRemovalMode()
        {
            placementController?.CancelPlacementMode();
            selectionController?.ClearSelection();
            _removalModeActive = true;
            _dragStartBlock = null;
            _preview?.Clear();
        }

        public void CancelRemovalMode()
        {
            _removalModeActive = false;
            _dragStartBlock = null;
            _preview?.Clear();
        }

        private void Update()
        {
            if (!_removalModeActive || worldBootstrap == null) return;

            if (UIPanelUtils.IsPointerOverBlockingUI(uiDocument))
            {
                if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
                    _dragStartBlock = null;
                _preview?.Clear();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelRemovalMode();
                return;
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                    _dragStartBlock = GetBlockUnderMouse();
                else if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    if (_dragStartBlock.HasValue)
                    {
                        var endBlock = GetBlockUnderMouse();
                        if (endBlock.HasValue && _executor != null)
                        {
                            var start = _dragStartBlock.Value;
                            var end = endBlock.Value;
                            if (start == end)
                                _executor.RemoveAtBlock(start.x, GetSurfaceY(start.x, start.z), start.z);
                            else
                                _executor.RemoveInArea(start, end);
                        }
                        _dragStartBlock = null;
                    }
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

        private int GetSurfaceY(int bx, int bz)
        {
            if (Grid == null) return 0;
            int topY = PlacementUtility.GetTopSolidY(Grid, bx, bz, Grid.Height);
            return topY >= 0 ? topY + 1 : 0;
        }

        private void UpdatePreview()
        {
            if (Grid == null || WaterConfig == null || _preview == null) return;

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);

            if (_dragStartBlock.HasValue)
            {
                var endBlock = GetBlockUnderMouse();
                if (endBlock.HasValue)
                {
                    var start = _dragStartBlock.Value;
                    var end = endBlock.Value;
                    _blocksBuffer.Clear();
                    foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, Grid, waterLevelY))
                        _blocksBuffer.Add((x, surfaceY, z));
                    _preview.SetBlocks(_blocksBuffer);
                }
                else
                    _preview.Clear();
            }
            else
            {
                if (_cachedCamera == null) _cachedCamera = Camera.main;
                if (_cachedCamera != null && PlacementUtility.TryRaycastTopSurface(_cachedCamera, Grid, WorldScale, waterLevelY, out var block, out _))
                {
                    if (worldBootstrap.HasRemovableAtBlock(block.bx, block.by, block.bz))
                    {
                        _blocksBuffer.Clear();
                        _blocksBuffer.Add((block.bx, block.by, block.bz));
                        _preview.SetBlocks(_blocksBuffer);
                    }
                    else
                        _preview.Clear();
                }
                else
                    _preview.Clear();
            }
        }

        private void OnDisable()
        {
            if (_removalModeActive)
                CancelRemovalMode();
        }
    }
}
