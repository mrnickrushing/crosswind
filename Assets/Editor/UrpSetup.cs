using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Crosswind.EditorTools
{
    /// <summary>One-off project setup: ambient occlusion on every renderer (contact shadows under aircraft, vehicles
    /// and along walls). Run from the CLI: <c>Crosswind.EditorTools.UrpSetup.Run()</c>. Safe to run again.</summary>
    public static class UrpSetup
    {
        public static string Run()
        {
            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (renderer == null || renderer.rendererFeatures.Exists(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion")) continue;
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) return "no SSAO type";
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "Ambient Occlusion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                var so = new SerializedObject(feature);
                Set(so, "m_Settings.Radius", 2.2f); Set(so, "m_Settings.Intensity", 1.6f); Set(so, "m_Settings.DirectLightingStrength", 0.35f); Set(so, "m_Settings.Falloff", 400f);
                so.ApplyModifiedProperties();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
                added++;
            }
            AssetDatabase.SaveAssets();
            return "ambient occlusion added to " + added + " renderer(s)";
        }

        static void Set(SerializedObject so, string path, float v) { var p = so.FindProperty(path); if (p != null) p.floatValue = v; }
    }
}
