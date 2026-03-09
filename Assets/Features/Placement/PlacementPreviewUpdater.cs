using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Updates placement preview based on camera raycast, entry, and drag state.
    /// Extracted from ObjectPlacementController.
    /// </summary>
    public class PlacementPreviewUpdater
    {
        private readonly WorldBootstrap _worldBootstrap;
        private readonly Camera _camera;
        private readonly WorldScale _worldScale;
        private (int x, int z)? _lastLineStart;
        private (int x, int z)? _lastLineEnd;
        private (int x, int z)? _lastAreaStart;
        private (int x, int z)? _lastAreaEnd;

        public PlacementPreviewUpdater(WorldBootstrap worldBootstrap, Camera camera)
        {
            _worldBootstrap = worldBootstrap;
            _camera = camera;
            _worldScale = new WorldScale(worldBootstrap?.WorldParameters != null ? worldBootstrap.WorldParameters.BlockScale : 1f);
        }

        public void Update(PlacedObjectEntry activeEntry, PlacementPreview preview, (int x, int z)? dragStartBlock,
            float rotationY, out (int x, int y, int z)? previewBlock, out bool previewValid)
        {
            previewBlock = null;
            previewValid = false;

            var grid = _worldBootstrap?.Grid;
            var waterConfig = _worldBootstrap?.WaterConfig;
            if (grid == null || waterConfig == null || activeEntry == null || preview == null) return;

            int waterLevelY = waterConfig.GetWaterLevelY(grid.Height);
            var isBlockValid = PlacementValidator.GetBlockValidatorForPlacement(_worldBootstrap, activeEntry);

            if (activeEntry.PlacementMode == PlacementMode.Area || activeEntry.PlacementMode == PlacementMode.Line)
            {
                if (dragStartBlock.HasValue)
                {
                    var endBlock = PlacementInputUtils.GetBlockUnderMouse(_camera, grid, _worldScale);
                    if (endBlock.HasValue)
                    {
                        if (activeEntry.PlacementMode == PlacementMode.Line)
                        {
                            if (_lastLineStart != dragStartBlock.Value || _lastLineEnd != endBlock.Value)
                            {
                                _lastLineStart = dragStartBlock.Value;
                                _lastLineEnd = endBlock.Value;
                                var skipPreview = PlacementValidator.ShouldSkipPreviewOnExistingRoads(activeEntry)
                                    ? (System.Func<int, int, int, bool>)((x, y, z) => _worldBootstrap.HasRoadAt(x, y, z))
                                    : null;
                                preview.SetLine(dragStartBlock.Value, endBlock.Value, grid, waterLevelY, isBlockValid, skipPreview);
                            }
                        }
                        else
                        {
                            if (_lastAreaStart != dragStartBlock.Value || _lastAreaEnd != endBlock.Value)
                            {
                                _lastAreaStart = dragStartBlock.Value;
                                _lastAreaEnd = endBlock.Value;
                                preview.SetAreaWithValidity(dragStartBlock.Value, endBlock.Value, grid, waterLevelY,
                                    isBlockValid, activeEntry.RandomRotation);
                            }
                        }
                    }
                    else
                    {
                        _lastLineStart = _lastLineEnd = _lastAreaStart = _lastAreaEnd = null;
                        preview.Clear();
                    }
                }
                else
                {
                    _lastLineStart = _lastLineEnd = _lastAreaStart = _lastAreaEnd = null;
                    if (PlacementUtility.TryRaycastTopSurface(_camera, grid, _worldScale, waterLevelY, out var block, out bool valid))
                    {
                        bool treeValid = valid && isBlockValid(block.bx, block.by, block.bz);
                        preview.SetSingle(block.bx, block.bz, block.by, 1, 1, 0f, treeValid);
                    }
                    else
                    {
                        preview.Clear();
                    }
                }
            }
            else
            {
                if (PlacementUtility.TryRaycastTopSurface(_camera, grid, _worldScale, waterLevelY, out var block, out bool valid))
                {
                    var (sizeX, sizeZ) = activeEntry.GetEffectiveArea(rotationY);
                    var (originX, originZ) = PlacementUtility.GetFootprintOrigin(block.bx, block.bz, sizeX, sizeZ);
                    previewValid = valid && PlacementValidator.IsAreaValidForPlacement(_worldBootstrap, originX, originZ, sizeX, sizeZ, block.by, activeEntry);
                    previewBlock = (originX, block.by, originZ);
                    preview.SetSingle(originX, originZ, block.by, sizeX, sizeZ, rotationY, previewValid);
                }
                else
                {
                    preview.Clear();
                }
            }
        }
    }
}
