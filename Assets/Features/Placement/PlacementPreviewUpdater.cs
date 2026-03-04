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
                            var skipPreview = PlacementValidator.ShouldSkipPreviewOnExistingRoads(activeEntry)
                                ? (System.Func<int, int, int, bool>)((x, y, z) => _worldBootstrap.HasRoadAt(x, y, z))
                                : null;
                            preview.SetLine(dragStartBlock.Value, endBlock.Value, grid, waterLevelY, isBlockValid, skipPreview);
                        }
                        else
                        {
                            preview.SetAreaWithValidity(dragStartBlock.Value, endBlock.Value, grid, waterLevelY,
                                isBlockValid, activeEntry.RandomRotation);
                        }
                    }
                    else
                    {
                        preview.Clear();
                    }
                }
                else
                {
                    if (PlacementUtility.TryRaycastTopSurface(_camera, grid, _worldScale, waterLevelY, out var block, out bool valid))
                    {
                        bool treeValid = valid && isBlockValid(block.bx, block.by, block.bz);
                        preview.SetSingle(block, 0f, treeValid);
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
                    previewValid = valid && PlacementValidator.IsBlockValidForPlacement(_worldBootstrap, block.bx, block.by, block.bz, activeEntry);
                    previewBlock = block;
                    preview.SetSingle(block, rotationY, previewValid);
                }
                else
                {
                    preview.Clear();
                }
            }
        }
    }
}
