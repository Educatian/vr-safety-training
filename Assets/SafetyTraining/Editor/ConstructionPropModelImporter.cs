using UnityEditor;

namespace SafetyTraining.Editor
{
    internal sealed class ConstructionPropModelImporter : AssetPostprocessor
    {
        const string Folder = "Assets/SafetyTraining/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.generateSecondaryUV = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
