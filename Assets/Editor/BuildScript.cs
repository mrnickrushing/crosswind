using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crosswind.EditorTools
{
    /// <summary>
    /// Builds the Xcode project into ./ios: <c>Crosswind.EditorTools.BuildScript.BuildIos()</c> (needs the iOS Build
    /// Support module). The game makes its shaders' materials at runtime, so the shaders it finds by name are added
    /// to Always Included Shaders first, or a device build would draw pink.
    /// </summary>
    public static class BuildScript
    {
        // Only small shaders go here: an always-included shader is built in every variant it has (URP/Lit has over half
        // a million). The URP ones the game makes materials from at runtime ship through material assets instead
        // (EnsureMaterials), so only the variants those use are built.
        static readonly string[] Shaders = { "Crosswind/VertexLit", "Skybox/Cubemap" };

        /// <summary>Resources/Materials/Surface.mat (URP Lit with a normal map) and Weather.mat (transparent particles): the runtime copies these.</summary>
        [MenuItem("Crosswind/Ensure runtime materials")]
        public static void EnsureMaterials()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Materials");
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Surface.mat") == null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/ConcreteNormal.jpg"));
                m.EnableKeyword("_NORMALMAP");
                AssetDatabase.CreateAsset(m, "Assets/Resources/Materials/Surface.mat");
            }
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Weather.mat") == null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.SetFloat("_Surface", 1); m.SetColor("_BaseColor", new Color(1, 1, 1, 0.55f));
                m.renderQueue = 3000; m.SetInt("_SrcBlend", 5); m.SetInt("_DstBlend", 10); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssetDatabase.CreateAsset(m, "Assets/Resources/Materials/Weather.mat");
            }
            AssetDatabase.SaveAssets();
        }

        public static string BuildIos()
        {
            Time.timeScale = 1f;
            UrpSetup.Run();
            EnsureMaterials();
            IncludeShaders();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = "ios", target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS, options = BuildOptions.None });
            string result = "[Crosswind] iOS build " + report.summary.result + " in " + report.summary.totalTime + ", " + report.summary.totalErrors + " errors, " + (report.summary.totalSize / 1048576) + " MB";
            Debug.Log(result);
            return result;
        }

        static void IncludeShaders()
        {
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { Debug.LogWarning("[Crosswind] shader not found: " + name); continue; }
                bool has = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
                if (has) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            }
            gs.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
