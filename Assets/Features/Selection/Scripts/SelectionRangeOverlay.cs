using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Displays operational range as a grid-aligned outline on the terrain when a building with range is selected.</summary>
    public class SelectionRangeOverlay : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private SelectionController selectionController;
        [SerializeField] private PlacedObjectRegistry registry;
        [SerializeField] private Color rangeColor = new Color(0.2f, 0.8f, 0.3f, 0.4f);
        [SerializeField] private float lineWidth = 0.05f;

        private GameObject _overlayRoot;
        private LineRenderer _lineRenderer;
        private Shader _rangeOverlayShader;
        private Shader _fallbackShader;
        private const int OutlineBufferCapacity = 256;
        private readonly List<Vector3> _positionsBuffer = new List<Vector3>(OutlineBufferCapacity);
        private Vector3[] _positionsArray;
        private Vector3 _lastCachedCenter;
        private int _lastCachedRangeCells = -1;
        private OperationalRangeType _lastCachedRangeType;

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
                _lastCachedRangeCells = -1;
                return;
            }

            var center = selected.position;
            var rangeCells = entry.OperationalRangeCells;
            var rangeType = entry.OperationalRangeType;
            if (center != _lastCachedCenter || rangeCells != _lastCachedRangeCells || rangeType != _lastCachedRangeType)
            {
                _lastCachedCenter = center;
                _lastCachedRangeCells = rangeCells;
                _lastCachedRangeType = rangeType;
                ShowOverlay(center, rangeCells, rangeType);
            }
        }

        private static readonly List<(int x, int z)> _verticesBuffer = new List<(int x, int z)>(256);

        private void ShowOverlay(Vector3 centerWorld, int rangeCells, OperationalRangeType rangeType)
        {
            var grid = worldBootstrap.Grid;
            var worldParams = worldBootstrap.WorldParameters;
            if (grid == null || worldParams == null || rangeCells < 0) return;

            var worldScale = new WorldScale(worldParams.BlockScale);
            var (cx, _, cz) = worldScale.WorldToBlock(centerWorld);

            OperationalRange.GetOutlineVertices(cx, cz, rangeCells, rangeType, grid.Width, grid.Depth, _verticesBuffer);
            if (_verticesBuffer.Count < 2)
            {
                HideOverlay();
                return;
            }

            EnsureOverlay();

            _positionsBuffer.Clear();
            for (int i = 0; i < _verticesBuffer.Count; i++)
            {
                var (x0, z0) = _verticesBuffer[i];
                var (x1, z1) = _verticesBuffer[(i + 1) % _verticesBuffer.Count];
                int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0), 1);
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                    int z = Mathf.RoundToInt(Mathf.Lerp(z0, z1, t));
                    int bx = Mathf.Clamp(x, 0, grid.Width - 1);
                    int bz = Mathf.Clamp(z, 0, grid.Depth - 1);
                    int topY = PlacementUtility.GetTopSolidY(grid, bx, bz, grid.Height);
                    int surfaceY = topY >= 0 ? topY + 1 : 0;
                    _positionsBuffer.Add(worldScale.BlockToWorld(x, surfaceY, z));
                }
            }

            if (_positionsArray == null || _positionsArray.Length < _positionsBuffer.Count)
                _positionsArray = new Vector3[_positionsBuffer.Count];
            _positionsBuffer.CopyTo(_positionsArray);

            _lineRenderer.positionCount = _positionsBuffer.Count;
            _lineRenderer.SetPositions(_positionsArray);
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
            if (_rangeOverlayShader == null)
                _rangeOverlayShader = Shader.Find(ShaderNames.RangeOverlay);
            if (_fallbackShader == null)
                _fallbackShader = Shader.Find(ShaderNames.Fallback);
            var shader = _rangeOverlayShader != null ? _rangeOverlayShader : _fallbackShader;
            _lineRenderer.material = new Material(shader != null ? shader : _fallbackShader);
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

        private void OnDestroy()
        {
            if (_lineRenderer != null && _lineRenderer.material != null)
            {
                Destroy(_lineRenderer.material);
            }
        }
    }
}
