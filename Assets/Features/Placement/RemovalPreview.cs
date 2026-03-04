using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Shows red preview for blocks/objects to be removed. Outlines objects, overlays quads for road blocks.</summary>
    public class RemovalPreview
    {
        private readonly WorldBootstrap _worldBootstrap;
        private readonly WorldScale _worldScale;
        private readonly SelectionOutlineRenderer _outlineRenderer = new();
        private readonly List<Transform> _outlinedTransforms = new();
        private readonly List<GameObject> _roadOverlays = new();
        private readonly List<Transform> _transformsBuffer = new(32);
        private readonly HashSet<Transform> _outlinedSet = new();
        private Material _roadOverlayMaterial;
        private Shader _defaultShader;
        private static readonly Color RemovalColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        private const float OutlineWidth = 4f;

        public RemovalPreview(WorldBootstrap worldBootstrap)
        {
            _worldBootstrap = worldBootstrap;
            _worldScale = new WorldScale(worldBootstrap?.WorldParameters != null ? worldBootstrap.WorldParameters.BlockScale : 1f);
        }

        public void SetBlocks(IEnumerable<(int x, int y, int z)> blocks)
        {
            Clear();

            if (_worldBootstrap == null) return;

            _transformsBuffer.Clear();
            _outlinedSet.Clear();

            foreach (var (bx, by, bz) in blocks)
            {
                if (!_worldBootstrap.HasRemovableAtBlock(bx, by, bz)) continue;

                _transformsBuffer.Clear();
                _worldBootstrap.GetTransformsAtBlock(bx, by, bz, _transformsBuffer);
                foreach (var t in _transformsBuffer)
                {
                    if (t != null && _outlinedSet.Add(t))
                        _outlineRenderer.ApplyHighlight(t, RemovalColor, OutlineWidth, null);
                }
                _outlinedTransforms.AddRange(_transformsBuffer);

                if (_worldBootstrap.HasRoadAt(bx, by, bz))
                {
                    var quad = CreateRoadOverlayQuad(bx, by, bz);
                    if (quad != null)
                        _roadOverlays.Add(quad);
                }
            }
        }

        public void Clear()
        {
            foreach (var t in _outlinedTransforms)
                _outlineRenderer.ClearHighlight(t);
            _outlinedTransforms.Clear();

            foreach (var go in _roadOverlays)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _roadOverlays.Clear();
        }

        /// <summary>Releases cached resources. Call when RemovalController is destroyed.</summary>
        public void Release()
        {
            Clear();
            if (_roadOverlayMaterial != null)
            {
                Object.Destroy(_roadOverlayMaterial);
                _roadOverlayMaterial = null;
            }
        }

        private GameObject CreateRoadOverlayQuad(int bx, int by, int bz)
        {
            var root = _worldBootstrap.transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "RemovalPreview_Road";
            go.transform.SetParent(root);
            go.transform.position = _worldScale.BlockToWorld(bx + 0.5f, by + 0.01f, bz + 0.5f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(_worldScale.BlockScale, _worldScale.BlockScale, 1f);

            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_defaultShader == null)
                    _defaultShader = Shader.Find("Sprites/Default");
                if (_roadOverlayMaterial == null && _defaultShader != null)
                {
                    _roadOverlayMaterial = new Material(_defaultShader);
                    _roadOverlayMaterial.color = RemovalColor;
                }
                if (_roadOverlayMaterial != null)
                    renderer.sharedMaterial = _roadOverlayMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go;
        }
    }
}
