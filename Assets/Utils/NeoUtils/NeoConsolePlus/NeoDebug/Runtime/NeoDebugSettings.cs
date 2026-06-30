#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Neo.Debugging
{
    internal static class NeoDebugSettings
    {
        internal const string RuntimeResourceName = "NeoDebugRuntimeSettings";
        internal const string EditorSettingsAssetPath = "Assets/NeoUtils/NeoUtilsGenerated/NeoConsolePlus/NeoDebug/Editor/NeoDebugEditorSettings.asset";

#if UNITY_EDITOR
        private static NeoDebugSettingsAsset editorSettings;
#elif DEVELOPMENT_BUILD
        private static NeoDebugSettingsAsset runtimeSettings;
        private static bool runtimeSettingsLoaded;
#endif

        public static bool CanLog()
        {
            if (!Enabled)
                return false;

#if UNITY_EDITOR
            return EnableInEditor;
#elif DEVELOPMENT_BUILD
            return EnableInDevelopmentBuild;
#else
            return false;
#endif
        }

        public static bool Enabled => SettingsOrDefaultBool(s => s.Enabled, NeoDebugSettingsAsset.DefaultEnabled);
        public static bool EnableInEditor => SettingsOrDefaultBool(s => s.EnableInEditor, NeoDebugSettingsAsset.DefaultEnableInEditor);
        public static bool EnableInDevelopmentBuild => SettingsOrDefaultBool(s => s.EnableInDevelopmentBuild, NeoDebugSettingsAsset.DefaultEnableInDevelopmentBuild);
        public static bool ShowScriptPrefix => SettingsOrDefaultBool(s => s.ShowScriptPrefix, NeoDebugSettingsAsset.DefaultShowScriptPrefix);
        public static bool ColorPrefix => SettingsOrDefaultBool(s => s.ColorPrefix, NeoDebugSettingsAsset.DefaultColorPrefix);
        public static bool PauseOnError => SettingsOrDefaultBool(s => s.PauseOnError, NeoDebugSettingsAsset.DefaultPauseOnError);

        public static Color LogPrefixColor => SettingsOrDefaultColor(s => s.LogPrefixColor, NeoDebugSettingsAsset.DefaultLogPrefixColor);
        public static Color WarningPrefixColor => SettingsOrDefaultColor(s => s.WarningPrefixColor, NeoDebugSettingsAsset.DefaultWarningPrefixColor);
        public static Color ErrorPrefixColor => SettingsOrDefaultColor(s => s.ErrorPrefixColor, NeoDebugSettingsAsset.DefaultErrorPrefixColor);

        private static bool SettingsOrDefaultBool(System.Func<NeoDebugSettingsAsset, bool> getter, bool defaultValue)
        {
            NeoDebugSettingsAsset settings = GetSettings();
            return settings != null ? getter(settings) : defaultValue;
        }


        private static Color SettingsOrDefaultColor(System.Func<NeoDebugSettingsAsset, Color> getter, Color defaultValue)
        {
            NeoDebugSettingsAsset settings = GetSettings();
            return settings != null ? getter(settings) : defaultValue;
        }

        private static NeoDebugSettingsAsset GetSettings()
        {
#if UNITY_EDITOR
            if (editorSettings != null)
                return editorSettings;

            editorSettings = AssetDatabase.LoadAssetAtPath<NeoDebugSettingsAsset>(EditorSettingsAssetPath);

            return editorSettings;
#elif DEVELOPMENT_BUILD
            if (runtimeSettingsLoaded)
                return runtimeSettings;

            runtimeSettingsLoaded = true;
            runtimeSettings = Resources.Load<NeoDebugSettingsAsset>(RuntimeResourceName);
            return runtimeSettings;
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        public static void InvalidateEditorCache()
        {
            editorSettings = null;
        }
#endif
    }
}
