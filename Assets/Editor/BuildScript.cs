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
        static readonly string[] Shaders = { "Crosswind/VertexLit", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Particles/Unlit", "Skybox/Cubemap" };

        public static string BuildIos()
        {
            Time.timeScale = 1f;
            UrpSetup.Run();
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
