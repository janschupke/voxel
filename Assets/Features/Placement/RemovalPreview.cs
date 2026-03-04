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

            var transformsBuffer = new List<Transform>(32);
            var outlinedSet = new HashSet<Transform>();

            foreach (var (bx, by, bz) in blocks)
            {
                if (!_worldBootstrap.HasRemovableAtBlock(bx, by, bz)) continue;

                transformsBuffer.Clear();
                _worldBootstrap.GetTransformsAtBlock(bx, by, bz, transformsBuffer);
                foreach (var t in transformsBuffer)
                {
                    if (t != null && outlinedSet.Add(t))
                        _outlineRenderer.ApplyHighlight(t, RemovalColor, OutlineWidth, null);
                }
                _outlinedTransforms.AddRange(transformsBuffer);

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
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = RemovalColor;
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go;
        }
    }
}
