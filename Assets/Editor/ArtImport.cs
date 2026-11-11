using UnityEditor;
using UnityEngine;

namespace Crosswind.EditorTools
{
    /// <summary>Import rules for the free models in Resources/Models: vertex-coloured meshes drawn with the game's own
    /// material, so no materials, cameras, lights or animation are imported with them.</summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importNormals = ModelImporterNormals.Import;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;       // the mesh itself faces +Z, with no rotated root to remember
        }
    }
}
