using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Renders outline highlights on selected/hovered objects. Extracted from SelectionController.
    /// </summary>
    public class SelectionOutlineRenderer
    {
        private readonly Dictionary<Transform, GameObject> _outlineObjects = new Dictionary<Transform, GameObject>();
        private readonly Shader _outlineShader;
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        public SelectionOutlineRenderer()
        {
            _outlineShader = Shader.Find(ShaderNames.SelectionOutline);
        }

        public void ApplyHighlight(Transform t, Color color, float width, System.Func<Transform, bool> isExcluded)
        {
            if (t == null || _outlineShader == null) return;
            if (isExcluded != null && isExcluded(t)) return;

            if (_outlineObjects.TryGetValue(t, out GameObject existing))
            {
                existing.SetActive(true);
                ApplyParams(existing, color, width);
                return;
            }

            var meshFilters = t.GetComponentsInChildren<MeshFilter>();
            if (meshFilters == null || meshFilters.Length == 0) return;

            var outlineRoot = new GameObject("SelectionOutline");
            outlineRoot.transform.SetParent(t, false);
            outlineRoot.transform.localPosition = Vector3.zero;
            outlineRoot.transform.localRotation = Quaternion.identity;
            outlineRoot.transform.localScale = Vector3.one;

            var mat = new Material(_outlineShader);
            mat.SetColor(OutlineColorId, color);
            mat.SetFloat(OutlineWidthId, width);

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                var child = new GameObject("OutlineMesh");
                child.transform.SetParent(outlineRoot.transform, false);
                child.transform.localPosition = t.InverseTransformPoint(mf.transform.position);
                child.transform.localRotation = Quaternion.Inverse(t.rotation) * mf.transform.rotation;
                child.transform.localScale = Vector3.one;

                var filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = mf.sharedMesh;

                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _outlineObjects[t] = outlineRoot;
        }

        private void ApplyParams(GameObject outlineRoot, Color color, float width)
        {
            foreach (var r in outlineRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial != null)
                {
                    r.sharedMaterial.SetColor(OutlineColorId, color);
                    r.sharedMaterial.SetFloat(OutlineWidthId, width);
                }
            }
        }

        public void ClearHighlight(Transform t)
        {
            if (t == null) return;
            if (_outlineObjects.TryGetValue(t, out GameObject outlineRoot))
            {
                _outlineObjects.Remove(t);
                Object.Destroy(outlineRoot);
            }
        }
    }
}
