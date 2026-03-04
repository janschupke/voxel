using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Creates a 1x1 quad mesh in the XZ plane (facing +Y) at runtime.
    /// Use when a built-in mesh reference may be invalid or orientation is wrong.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class QuadMeshCreator : MonoBehaviour
    {
        private static Mesh _sharedMesh;

        private void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh != null) return;

            if (_sharedMesh == null)
            {
                _sharedMesh = new Mesh { name = "ProceduralQuad" };
                _sharedMesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0, -0.5f),
                    new Vector3(0.5f, 0, -0.5f),
                    new Vector3(-0.5f, 0, 0.5f),
                    new Vector3(0.5f, 0, 0.5f)
                };
                _sharedMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                _sharedMesh.normals = new[]
                {
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up
                };
                _sharedMesh.uv = new[]
                {
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1)
                };
                _sharedMesh.RecalculateBounds();
            }

            mf.sharedMesh = _sharedMesh;
        }
    }
}
