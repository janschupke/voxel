using UnityEditor;

namespace Voxel.Editor
{
    /// <summary>
    /// Ensures MagicaVoxel OBJ imports use correct settings for Structures/Models.
    /// </summary>
    public class VoxelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Features/Structures/Models/"))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileUnits = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
        }
    }
}
