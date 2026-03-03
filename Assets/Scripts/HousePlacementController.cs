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
        private GameObject _previewInstance;
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
            DestroyPreview();
            _previewBlock = null;
        }

        private void UpdatePreview()
        {
            if (Grid == null || HousePrefab == null || WaterConfig == null)
            {
                DestroyPreview();
                _previewBlock = null;
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                DestroyPreview();
                _previewBlock = null;
                return;
            }

            int waterLevelY = WaterConfig.GetWaterLevelY(Grid.Height);
            if (TryRaycastTopSurface(cam, Grid, WorldScale, waterLevelY, out var block, out bool valid))
            {
                if (!_previewBlock.HasValue || _previewBlock.Value != block || _previewValid != valid)
                {
                    _previewBlock = block;
                    _previewValid = valid;
                    RestoreHiddenTrees();
                    if (valid)
                        HideTreesAtBlock(block);
                }
                EnsurePreview(valid);
                if (_previewInstance != null)
                {
                    var pos = WorldScale.BlockToWorld(block.bx + 0.5f, block.by, block.bz + 0.5f);
                    _previewInstance.transform.position = pos;
                    _previewInstance.transform.rotation = Quaternion.Euler(0f, _rotationY, 0f);
                }
            }
            else
            {
                _previewBlock = null;
                _previewValid = false;
                RestoreHiddenTrees();
                DestroyPreview();
            }
        }

        private void EnsurePreview(bool valid)
        {
            if (_previewInstance != null)
            {
                UpdatePreviewMaterials(valid);
                return;
            }
            if (HousePrefab == null) return;

            _previewInstance = Instantiate(HousePrefab);
            _previewInstance.name = "HousePreview";

            var scale = WorldScale.ScaleVectorForBlockSizedPrefab(2f);
            _previewInstance.transform.localScale = scale;

            foreach (var mr in _previewInstance.GetComponentsInChildren<MeshRenderer>())
            {
                var mat = new Material(mr.sharedMaterial);
                mr.sharedMaterial = mat;
            }
            UpdatePreviewMaterials(valid);

            foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        private void UpdatePreviewMaterials(bool valid)
        {
            if (_previewInstance == null) return;

            Color tint = valid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);

            foreach (var mr in _previewInstance.GetComponentsInChildren<MeshRenderer>())
            {
                var mat = mr.sharedMaterial;
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", tint);
                else
                    mat.color = tint;
                mat.renderQueue = 3000;
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
            }
        }

        private void DestroyPreview()
        {
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
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

        private static bool TryRaycastTopSurface(Camera cam, VoxelGrid grid, WorldScale scale, int waterLevelY, out (int bx, int by, int bz) block, out bool valid)
        {
            block = default;
            valid = false;

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
                valid = topY >= waterLevelY;
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
