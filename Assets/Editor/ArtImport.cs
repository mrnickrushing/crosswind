using UnityEditor;
using UnityEngine;

namespace Crosswind.EditorTools
{
    /// <summary>Import rules for the free models in Resources/Models: vertex-coloured meshes drawn with the game's own
    /// material, so no materials, cameras, lights or animation are imported with them.</summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var t = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Resources/Sky/"))
            {
                // The HDRI sky (Poly Haven, CC0): a cubemap for the skybox, the ambient light and reflections.
                t.textureShape = TextureImporterShape.TextureCube;
                t.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                t.sRGBTexture = false; t.mipmapEnabled = true; t.maxTextureSize = 1024;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (assetPath.StartsWith("Assets/Resources/UI/Faces/") || assetPath.StartsWith("Assets/Resources/UI/Icons/"))
            {
                // Painted portraits and the picture icons: shown well under their size, so mipmapped, and compressed (there are many).
                t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single; t.mipmapEnabled = true; t.alphaIsTransparency = true;
                t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (assetPath.StartsWith("Assets/Resources/UI/"))
            {
                t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single; t.mipmapEnabled = false; t.alphaIsTransparency = true;
                t.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!assetPath.StartsWith("Assets/Resources/Textures/") && !assetPath.StartsWith("Assets/Resources/Models/")) return;
            bool normal = assetPath.Contains("Normal") || assetPath.Contains("_normal");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal;
            t.wrapMode = TextureWrapMode.Repeat;
            t.mipmapEnabled = true; t.anisoLevel = 8; t.maxTextureSize = 1024;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        void OnPreprocessAudio()
        {
            // Music is long: stream it from disk rather than hold it decoded in memory.
            if (!assetPath.StartsWith("Assets/Resources/Audio/Music/")) return;
            var a = (AudioImporter)assetImporter;
            a.loadInBackground = true;
            var st = a.defaultSampleSettings;
            st.loadType = AudioClipLoadType.Streaming; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.55f;
            a.defaultSampleSettings = st;
        }

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
