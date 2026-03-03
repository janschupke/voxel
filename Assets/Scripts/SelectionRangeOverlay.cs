using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    /// <summary>Displays operational range as an outline on the terrain when a building with range is selected.</summary>
    public class SelectionRangeOverlay : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private SelectionController selectionController;
        [SerializeField] private PlacedObjectRegistry registry;
        [SerializeField] private Color rangeColor = new Color(0.2f, 0.8f, 0.3f, 0.4f);
        [SerializeField] private float lineWidth = 0.05f;

        private GameObject _overlayRoot;
        private LineRenderer _lineRenderer;
        private const int CircleSegments = 64;

        private void Start()
        {
            if (worldBootstrap == null) worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (selectionController == null) selectionController = FindAnyObjectByType<SelectionController>();
            if (registry == null && worldBootstrap != null) registry = worldBootstrap.PlacedObjectRegistry;
        }

        private void LateUpdate()
        {
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (worldBootstrap == null || selectionController == null || registry == null)
            {
                HideOverlay();
                return;
            }

            var selected = selectionController.SelectedObject;
            var entryName = selectionController.SelectedEntryName;
            var entry = !string.IsNullOrEmpty(entryName) ? registry.GetByName(entryName) : null;

            if (selected == null || entry == null || !entry.HasOperationalRange)
            {
                HideOverlay();
                return;
            }

            ShowOverlay(selected.position, entry.OperationalRangeInBlocks);
        }

        private void ShowOverlay(Vector3 centerWorld, float radiusBlocks)
        {
            var grid = worldBootstrap.Grid;
            var worldParams = worldBootstrap.WorldParameters;
            if (grid == null || worldParams == null) return;

            var worldScale = new WorldScale(worldParams.BlockScale);
            var (cx, _, cz) = worldScale.WorldToBlock(centerWorld);

            EnsureOverlay();

            var positions = new List<Vector3>();
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (float)i / CircleSegments * 2f * Mathf.PI;
                float bx = cx + radiusBlocks * Mathf.Cos(angle);
                float bz = cz + radiusBlocks * Mathf.Sin(angle);

                int ix = Mathf.FloorToInt(bx);
                int iz = Mathf.FloorToInt(bz);

                if (ix < 0 || ix >= grid.Width || iz < 0 || iz >= grid.Depth)
                    continue;

                int topY = PlacementUtility.GetTopSolidY(grid, ix, iz, grid.Height);
                if (topY < 0) continue;

                int surfaceY = topY + 1;
                var worldPos = worldScale.BlockToWorld(bx, surfaceY, bz);
                positions.Add(worldPos);
            }

            if (positions.Count < 2)
            {
                HideOverlay();
                return;
            }

            _lineRenderer.positionCount = positions.Count;
            _lineRenderer.SetPositions(positions.ToArray());
            _overlayRoot.SetActive(true);
        }

        private void EnsureOverlay()
        {
            if (_overlayRoot != null) return;

            _overlayRoot = new GameObject("SelectionRangeOverlay");
            _overlayRoot.transform.SetParent(transform);

            _lineRenderer = _overlayRoot.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = true;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            var shader = Shader.Find("Voxel/RangeOverlay");
            _lineRenderer.material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            if (_lineRenderer.material.HasProperty("_Color"))
                _lineRenderer.material.SetColor("_Color", rangeColor);
            else
                _lineRenderer.material.color = rangeColor;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
        }

        private void HideOverlay()
        {
            if (_overlayRoot != null)
                _overlayRoot.SetActive(false);
        }
    }
}
