#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Neo.ConsolePlus.Editor
{
    [EditorWindowTitle(title = "NeoConsole+ Settings")]
    internal sealed class NeoConsolePlusSettingsWindow : EditorWindow
    {
        private const float ShortcutResetMaxWidth = 260f;
        private const float ShortcutKeyMaxWidth = ShortcutResetMaxWidth * 0.5f;

        private static readonly GUIContent WindowTitle = new GUIContent("NeoConsole+ Settings");
        private const string DebugCommandsAssetPath = "Assets/DebugCommands.cs";

        private static readonly string ExampleCode =
            "using Neo.ConsolePlus;\n" +
            "using UnityEngine;\n\n" +
            "public sealed class DebugCommands : MonoBehaviour\n" +
            "{\n" +
            "    [NeoCommand(\"debug.spawn_cube\", \"Spawns a cube in front of the main camera.\")]\n" +
            "    private void SpawnCube()\n" +
            "    {\n" +
            "        Camera cam = Camera.main;\n" +
            "        Vector3 position = cam != null ? cam.transform.position + cam.transform.forward * 5f : Vector3.zero;\n" +
            "        GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = position;\n" +
            "    }\n" +
            "}\n\n" +
            "Use commands with '/':\n" +
            "/debug.spawn_cube";

        private int shortcutCaptureTarget;
        private Vector2 scroll;

        [MenuItem("Tools/NeoUtils/NeoConsolePlus/Open Settings")]
        public static void OpenFromToolsMenu()
        {
            OpenWindow();
        }

        [MenuItem("Window/General/NeoConsole+ Settings")]
        private static void OpenFromWindowMenu()
        {
            OpenWindow();
        }

        internal static NeoConsolePlusSettingsWindow OpenWindow()
        {
            NeoConsolePlusSettingsWindow window = GetWindow<NeoConsolePlusSettingsWindow>(WindowTitle.text);
            window.titleContent = WindowTitle;
            window.minSize = new Vector2(380f, 420f);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            titleContent = WindowTitle;
            NeoConsolePlusEditorSettings.EnsureDefaultsInitialized();
            ApplyRuntimeSettings();
        }

        private void OnGUI()
        {
            HandleShortcutCaptureEvent();

            EditorGUILayout.BeginVertical();
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawRuntimeOverlaySection();
            DrawLogBufferSection();
            DrawConsoleWindowSection();
            DrawCommandSection();
            DrawMaintenanceSection();
            DrawExampleSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("NeoConsole+ Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Configure runtime overlay, logs, command diagnostics and editor behavior.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeOverlaySection()
        {
            DrawSectionTitle("Runtime Overlay");

            bool autoCreateOverlay = !NeoConsolePlusDefineUtility.IsRuntimeOverlayAutoCreationDisabled;
            EditorGUI.BeginChangeCheck();
            autoCreateOverlay = EditorGUILayout.ToggleLeft("Automatically create runtime overlay", autoCreateOverlay);
            if (EditorGUI.EndChangeCheck())
                NeoConsolePlusDefineUtility.IsRuntimeOverlayAutoCreationDisabled = !autoCreateOverlay;

            EditorGUI.BeginChangeCheck();
            bool openOnError = EditorGUILayout.ToggleLeft("Open runtime overlay when Error/Exception/Assert is received", NeoConsolePlusEditorSettings.OpenRuntimeOverlayOnError);
            if (EditorGUI.EndChangeCheck())
            {
                NeoConsolePlusEditorSettings.OpenRuntimeOverlayOnError = openOnError;
                ApplyRuntimeSettings();
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Current shortcut", NeoRuntimeOverlayShortcut.DisplayText);
            DrawShortcutKeyField("Primary open key", NeoRuntimeOverlayShortcut.PrimaryKey, 1);
            DrawShortcutKeyField("Secondary open key", NeoRuntimeOverlayShortcut.SecondaryKey, 2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Shortcut", GUILayout.MinWidth(120f), GUILayout.MaxWidth(ShortcutResetMaxWidth)))
            {
                NeoRuntimeOverlayShortcut.ResetToDefaults();
                shortcutCaptureTarget = 0;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Active build target", NeoConsolePlusDefineUtility.CurrentBuildTargetDisplayName);
            EditorGUILayout.HelpBox(autoCreateOverlay
                ? "Runtime overlay auto creation is enabled for the active build target."
                : "Runtime overlay auto creation is disabled for the active build target.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawLogBufferSection()
        {
            DrawSectionTitle("Log Buffer");

            EditorGUI.BeginChangeCheck();
            int maxEntries = EditorGUILayout.IntSlider("Max stored log entries", NeoConsolePlusEditorSettings.MaxLogEntries, NeoConsolePlusEditorSettings.MinLogEntries, NeoConsolePlusEditorSettings.MaxLogEntriesLimit);
            if (EditorGUI.EndChangeCheck())
            {
                NeoConsolePlusEditorSettings.MaxLogEntries = maxEntries;
                ApplyRuntimeSettings();
            }

            EditorGUILayout.LabelField("Current buffer", NeoConsoleLogBuffer.Count + " / " + NeoConsoleLogBuffer.MaxEntries + " entries", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear NeoConsole Logs"))
                NeoConsoleLogBuffer.Clear();
            if (GUILayout.Button("Clear Including Compiler Errors"))
                NeoConsoleLogBuffer.Clear(true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawConsoleWindowSection()
        {
            DrawSectionTitle("Editor Console Window");

            NeoConsolePlusEditorSettings.ShowNeoCommandLogs = EditorGUILayout.ToggleLeft("Show NeoCommand execution logs", NeoConsolePlusEditorSettings.ShowNeoCommandLogs);

            EditorGUI.BeginChangeCheck();
            bool useCustomColor = EditorGUILayout.ToggleLeft("Use custom color for regular Console prefixes", NeoConsolePlusEditorSettings.UseCustomConsoleLogColor);
            if (EditorGUI.EndChangeCheck())
                NeoConsolePlusEditorSettings.UseCustomConsoleLogColor = useCustomColor;

            using (new EditorGUI.DisabledScope(!NeoConsolePlusEditorSettings.UseCustomConsoleLogColor))
            {
                NeoConsolePlusEditorSettings.ConsoleLogColor = EditorGUILayout.ColorField("Console Log Prefix Color", NeoConsolePlusEditorSettings.ConsoleLogColor);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset Prefix Color", GUILayout.MinWidth(140f), GUILayout.MaxWidth(200f)))
                    NeoConsolePlusEditorSettings.ResetConsoleLogColor();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCommandSection()
        {
            DrawSectionTitle("Commands");
            EditorGUILayout.HelpBox("Commands must be typed with '/'. Attribute names must not start with '/' and must not contain whitespace. Use '.', '_' or '-' for command names.", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Command Registry"))
                NeoCommandRegistry.Refresh();
            if (GUILayout.Button("Open NeoConsole"))
                NeoConsoleWindow.OpenFloating();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawMaintenanceSection()
        {
            DrawSectionTitle("Clear Options");
            NeoConsolePlusEditorSettings.ClearOnPlay = EditorGUILayout.ToggleLeft("Clear on Play", NeoConsolePlusEditorSettings.ClearOnPlay);
            NeoConsolePlusEditorSettings.ClearOnBuild = EditorGUILayout.ToggleLeft("Clear on Build", NeoConsolePlusEditorSettings.ClearOnBuild);
            NeoConsolePlusEditorSettings.ClearOnRecompile = EditorGUILayout.ToggleLeft("Clear on Recompile", NeoConsolePlusEditorSettings.ClearOnRecompile);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Player Settings"))
                SettingsService.OpenProjectSettings("Project/Player");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawExampleSection()
        {
            DrawSectionTitle("Example");
            EditorGUILayout.HelpBox("Generate a ready-to-edit DebugCommands.cs file in Assets. The generated example includes NeoCommand, NeoCommandEditorOnly, NeoCommandRuntimeOnly and an instance command target test.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(File.Exists(DebugCommandsAssetPath) ? "Ping DebugCommands.cs" : "Generate DebugCommands.cs"))
                GenerateOrPingDebugCommandsFile();

            using (new EditorGUI.DisabledScope(!File.Exists(DebugCommandsAssetPath)))
            {
                if (GUILayout.Button("Open DebugCommands.cs"))
                    OpenDebugCommandsFile();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            GUIStyle style = CreateReadOnlyCodeStyle();
            float height = Mathf.Max(150f, style.CalcHeight(new GUIContent(ExampleCode), Mathf.Max(240f, position.width - 36f)) + 18f);
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect labelRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            GUI.Label(labelRect, ExampleCode, style);
            EditorGUILayout.EndVertical();
        }

        private static void GenerateOrPingDebugCommandsFile()
        {
            if (!File.Exists(DebugCommandsAssetPath))
            {
                File.WriteAllText(DebugCommandsAssetPath, BuildDebugCommandsTemplate(), Encoding.UTF8);
                AssetDatabase.ImportAsset(DebugCommandsAssetPath);
                AssetDatabase.Refresh();
            }

            PingDebugCommandsFile();
        }

        private static void OpenDebugCommandsFile()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(DebugCommandsAssetPath);
            if (asset == null)
            {
                GenerateOrPingDebugCommandsFile();
                return;
            }

            AssetDatabase.OpenAsset(asset);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void PingDebugCommandsFile()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(DebugCommandsAssetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static string BuildDebugCommandsTemplate()
        {
            return
                "using Neo.ConsolePlus;\n" +
                "using UnityEngine;\n\n" +
                "public sealed class DebugCommands : MonoBehaviour\n" +
                "{\n" +
                "    private GameObject spawnedCube;\n\n" +
                "    [NeoCommand(\"debug.spawn_cube\", \"Spawns a cube in front of the main camera.\")]\n" +
                "    private void SpawnCube()\n" +
                "    {\n" +
                "        Camera cam = Camera.main;\n" +
                "        Vector3 position = cam != null ? cam.transform.position + cam.transform.forward * 5f : Vector3.zero;\n" +
                "        spawnedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);\n" +
                "        spawnedCube.name = \"Neo Debug Cube\";\n" +
                "        spawnedCube.transform.position = position;\n" +
                "    }\n\n" +
                "    [NeoCommand(\"debug.move_self\", \"Moves this command component GameObject. Tests instance targets.\")]\n" +
                "    private void MoveSelf(float x, float y, float z)\n" +
                "    {\n" +
                "        transform.position = new Vector3(x, y, z);\n" +
                "    }\n\n" +
                "    [NeoCommandRuntimeOnly(\"debug.runtime_message\", \"Runs only in the Runtime Console.\")]\n" +
                "    private static string RuntimeMessage()\n" +
                "    {\n" +
                "        return \"Runtime-only command executed.\";\n" +
                "    }\n\n" +
                "    [NeoCommandEditorOnly(\"debug.editor_message\", \"Runs only in the Editor Console.\")]\n" +
                "    private static string EditorMessage()\n" +
                "    {\n" +
                "        return \"Editor-only command executed.\";\n" +
                "    }\n" +
                "}\n";
        }

        private void DrawSectionTitle(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawShortcutKeyField(string label, KeyCode key, int target)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.MinWidth(130f), GUILayout.ExpandWidth(true));
            string buttonLabel = shortcutCaptureTarget == target ? "Press any key..." : NeoRuntimeOverlayShortcut.FormatKey(key);
            if (GUILayout.Button(buttonLabel, GUILayout.MinWidth(84f), GUILayout.MaxWidth(ShortcutKeyMaxWidth)))
                shortcutCaptureTarget = target;
            EditorGUILayout.EndHorizontal();
        }

        private void HandleShortcutCaptureEvent()
        {
            if (shortcutCaptureTarget == 0)
                return;

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode == KeyCode.None)
                return;

            if (shortcutCaptureTarget == 1)
                NeoRuntimeOverlayShortcut.SetPrimaryKey(current.keyCode);
            else if (shortcutCaptureTarget == 2)
                NeoRuntimeOverlayShortcut.SetSecondaryKey(current.keyCode);

            shortcutCaptureTarget = 0;
            current.Use();
            Repaint();
        }

        private static void ApplyRuntimeSettings()
        {
            NeoConsoleLogBuffer.MaxEntries = NeoConsolePlusEditorSettings.MaxLogEntries;
            NeoRuntimeOverlay.OpenOnError = NeoConsolePlusEditorSettings.OpenRuntimeOverlayOnError;
        }

        private static GUIStyle CreateReadOnlyCodeStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = false;
            style.richText = false;
            style.font = EditorStyles.miniFont;
            return style;
        }
    }
}
#endif
