using UnityEditor;

namespace Voxel.Editor
{
    /// <summary>
    /// Editor menu for Voxel. Placeholder for future editor tools.
    /// </summary>
    public static class VoxelEditorMenu
    {
        [MenuItem("Voxel/About")]
        private static void About()
        {
            EditorUtility.DisplayDialog("Voxel", "3D top-down voxel game.", "OK");
        }
    }
}
