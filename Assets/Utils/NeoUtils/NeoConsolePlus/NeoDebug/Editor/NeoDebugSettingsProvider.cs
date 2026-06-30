#if UNITY_EDITOR
using System.Collections.Generic;
using Neo.Debugging;
using UnityEditor;
using UnityEngine;

namespace Neo.Debugging.Editor
{
    internal static class NeoDebugSettingsProvider
    {
        private const string SettingsPath = "Project/NeoUtils/NeoConsolePlus/NeoDebug";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "NeoDebug",
                keywords = new HashSet<string>
                {
                    "Neo", "NeoUtils", "NeoDebug", "Debug", "Log", "Warning", "Error", "Prefix", "Pause", "Color"
                },
                guiHandler = DrawSettings
            };
        }

        private static void DrawSettings(string searchContext)
        {
            NeoDebugSettingsAsset settings = NeoDebugEditorSettingsUtility.GetOrCreateEditorSettings();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("NeoDebug", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Regular release builds are always stripped by Conditional attributes. NeoDebug calls and their arguments are not compiled into non-development builds.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(settings, "NeoDebug Settings");

            settings.Enabled = EditorGUILayout.ToggleLeft("Enable NeoDebug", settings.Enabled);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            settings.EnableInEditor = EditorGUILayout.ToggleLeft("Enable In Editor", settings.EnableInEditor);
            settings.EnableInDevelopmentBuild = EditorGUILayout.ToggleLeft("Enable In Development Build", settings.EnableInDevelopmentBuild);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ToggleLeft("Enable In Release Build", false);
            }

            EditorGUILayout.HelpBox(
                "Release logging is intentionally not toggleable from settings. This prevents debug calls and sensitive log arguments from leaking into final builds.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Formatting", EditorStyles.boldLabel);
            settings.ShowScriptPrefix = EditorGUILayout.ToggleLeft("Show Script Prefix", settings.ShowScriptPrefix);

            using (new EditorGUI.DisabledScope(!settings.ShowScriptPrefix))
            {
                settings.ColorPrefix = EditorGUILayout.ToggleLeft("Color Prefix", settings.ColorPrefix);

                using (new EditorGUI.DisabledScope(!settings.ColorPrefix))
                {
                    settings.LogPrefixColor = EditorGUILayout.ColorField("Log Prefix Color", settings.LogPrefixColor);
                    settings.WarningPrefixColor = EditorGUILayout.ColorField("Warning Prefix Color", settings.WarningPrefixColor);
                    settings.ErrorPrefixColor = EditorGUILayout.ColorField("Error Prefix Color", settings.ErrorPrefixColor);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Editor Behavior", EditorStyles.boldLabel);
            settings.PauseOnError = EditorGUILayout.ToggleLeft("Pause On Error", settings.PauseOnError);

            if (EditorGUI.EndChangeCheck())
                NeoDebugEditorSettingsUtility.SaveEditorSettings(settings);

            EditorGUILayout.Space(10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset NeoDebug Settings", GUILayout.Width(180f)))
                {
                    Undo.RecordObject(settings, "Reset NeoDebug Settings");
                    settings.ResetToDefaults();
                    NeoDebugEditorSettingsUtility.SaveEditorSettings(settings);
                }

                if (GUILayout.Button("Delete Temporary Build Asset", GUILayout.Width(190f)))
                    NeoDebugEditorSettingsUtility.DeleteRuntimeSettingsAsset();
            }
        }
    }
}
#endif
