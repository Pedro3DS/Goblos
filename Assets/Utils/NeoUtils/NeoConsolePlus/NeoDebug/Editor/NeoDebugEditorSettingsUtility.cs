#if UNITY_EDITOR
using System.IO;
using Neo.Debugging;
using UnityEditor;
using UnityEngine;

namespace Neo.Debugging.Editor
{
    internal static class NeoDebugEditorSettingsUtility
    {
        private const string RuntimeResourcesDirectory = "Assets/NeoUtils/NeoUtilsGenerated/NeoConsolePlus/NeoDebug/Resources";
        private const string RuntimeSettingsAssetPath = RuntimeResourcesDirectory + "/NeoDebugRuntimeSettings.asset";

        public static NeoDebugSettingsAsset GetOrCreateEditorSettings()
        {
            NeoDebugSettingsAsset settings = AssetDatabase.LoadAssetAtPath<NeoDebugSettingsAsset>(NeoDebugSettings.EditorSettingsAssetPath);

            if (settings != null)
                return settings;

            string directory = Path.GetDirectoryName(NeoDebugSettings.EditorSettingsAssetPath);

            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            settings = ScriptableObject.CreateInstance<NeoDebugSettingsAsset>();
            settings.ResetToDefaults();

            AssetDatabase.CreateAsset(settings, NeoDebugSettings.EditorSettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            NeoDebugSettings.InvalidateEditorCache();

            return settings;
        }

        public static void SaveEditorSettings(NeoDebugSettingsAsset settings)
        {
            if (settings == null)
                return;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            NeoDebugSettings.InvalidateEditorCache();
        }

        public static void WriteDevelopmentBuildRuntimeSettings()
        {
            NeoDebugSettingsAsset editorSettings = GetOrCreateEditorSettings();

            if (!AssetDatabase.IsValidFolder(RuntimeResourcesDirectory))
            {
                Directory.CreateDirectory(RuntimeResourcesDirectory);
                AssetDatabase.Refresh();
            }

            NeoDebugSettingsAsset runtimeSettings = AssetDatabase.LoadAssetAtPath<NeoDebugSettingsAsset>(RuntimeSettingsAssetPath);

            if (runtimeSettings == null)
            {
                runtimeSettings = ScriptableObject.CreateInstance<NeoDebugSettingsAsset>();
                AssetDatabase.CreateAsset(runtimeSettings, RuntimeSettingsAssetPath);
            }

            runtimeSettings.CopyFrom(editorSettings);
            EditorUtility.SetDirty(runtimeSettings);
            AssetDatabase.SaveAssets();
        }

        public static void DeleteRuntimeSettingsAsset()
        {
            AssetDatabase.DeleteAsset(RuntimeSettingsAssetPath);

            if (AssetDatabase.IsValidFolder(RuntimeResourcesDirectory))
            {
                string[] children = AssetDatabase.FindAssets(string.Empty, new[] { RuntimeResourcesDirectory });

                if (children == null || children.Length == 0)
                    AssetDatabase.DeleteAsset(RuntimeResourcesDirectory);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif
