#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Neo.ConsolePlus.Editor
{
    internal static class NeoConsolePlusEditorSettings
    {
        private const string ShowNeoCommandLogsKey = "Neo.ConsolePlus.ShowNeoCommandLogs";
        private const string UseCustomConsoleLogColorKey = "Neo.ConsolePlus.UseCustomConsoleLogColor";
        private const string ConsoleLogColorKey = "Neo.ConsolePlus.ConsoleLogColor";
        private const string DetailsHeightKey = "Neo.ConsolePlus.LogDetailsHeight";
        private const string MaxLogEntriesKey = "Neo.ConsolePlus.MaxLogEntries";
        private const string OpenRuntimeOverlayOnErrorKey = "Neo.ConsolePlus.OpenRuntimeOverlayOnError";
        private const string ClearOnPlayKey = "Neo.ConsolePlus.ClearOnPlay";
        private const string ClearOnBuildKey = "Neo.ConsolePlus.ClearOnBuild";
        private const string ClearOnRecompileKey = "Neo.ConsolePlus.ClearOnRecompile";
        private const string DefaultsInitializedKey = "Neo.ConsolePlus.DefaultsInitializedV3";

        private const int DefaultMaxLogEntries = 500;
        private const int MinimumMaxLogEntries = 50;
        private const int MaximumMaxLogEntries = 5000;

        private static readonly Color DefaultConsoleLogColor = new Color(0.3254902f, 0.6431373f, 0.3686275f, 1f); // #53A45E

        public static void EnsureDefaultsInitialized()
        {
            if (EditorPrefs.GetBool(DefaultsInitializedKey, false))
                return;

            UseCustomConsoleLogColor = true;
            ConsoleLogColor = DefaultConsoleLogColor;
            MaxLogEntries = DefaultMaxLogEntries;
            OpenRuntimeOverlayOnError = true;
            EditorPrefs.SetBool(DefaultsInitializedKey, true);
        }

        public static bool ShowNeoCommandLogs
        {
            get { return EditorPrefs.GetBool(ShowNeoCommandLogsKey, true); }
            set { EditorPrefs.SetBool(ShowNeoCommandLogsKey, value); }
        }

        public static bool UseCustomConsoleLogColor
        {
            get { return EditorPrefs.GetBool(UseCustomConsoleLogColorKey, true); }
            set { EditorPrefs.SetBool(UseCustomConsoleLogColorKey, value); }
        }

        public static Color ConsoleLogColor
        {
            get { return LoadColor(ConsoleLogColorKey, DefaultConsoleLogColor); }
            set { SaveColor(ConsoleLogColorKey, value); }
        }

        public static Color DefaultLogColor
        {
            get { return DefaultConsoleLogColor; }
        }

        public static void ResetConsoleLogColor()
        {
            UseCustomConsoleLogColor = true;
            ConsoleLogColor = DefaultConsoleLogColor;
        }

        public static float DetailsHeight
        {
            get { return EditorPrefs.GetFloat(DetailsHeightKey, 180f); }
            set { EditorPrefs.SetFloat(DetailsHeightKey, Mathf.Clamp(value, 96f, 600f)); }
        }


        public static int MaxLogEntries
        {
            get { return Mathf.Clamp(EditorPrefs.GetInt(MaxLogEntriesKey, DefaultMaxLogEntries), MinimumMaxLogEntries, MaximumMaxLogEntries); }
            set { EditorPrefs.SetInt(MaxLogEntriesKey, Mathf.Clamp(value, MinimumMaxLogEntries, MaximumMaxLogEntries)); }
        }

        public static bool OpenRuntimeOverlayOnError
        {
            get { return EditorPrefs.GetBool(OpenRuntimeOverlayOnErrorKey, true); }
            set { EditorPrefs.SetBool(OpenRuntimeOverlayOnErrorKey, value); }
        }

        public static int MinLogEntries
        {
            get { return MinimumMaxLogEntries; }
        }

        public static int MaxLogEntriesLimit
        {
            get { return MaximumMaxLogEntries; }
        }

        public static bool ClearOnPlay
        {
            get { return EditorPrefs.GetBool(ClearOnPlayKey, true); }
            set { EditorPrefs.SetBool(ClearOnPlayKey, value); }
        }

        public static bool ClearOnBuild
        {
            get { return EditorPrefs.GetBool(ClearOnBuildKey, false); }
            set { EditorPrefs.SetBool(ClearOnBuildKey, value); }
        }

        public static bool ClearOnRecompile
        {
            get { return EditorPrefs.GetBool(ClearOnRecompileKey, false); }
            set { EditorPrefs.SetBool(ClearOnRecompileKey, value); }
        }

        private static Color LoadColor(string key, Color fallback)
        {
            string html = EditorPrefs.GetString(key, ColorUtility.ToHtmlStringRGBA(fallback));
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + html, out color))
                return color;

            return fallback;
        }

        private static void SaveColor(string key, Color color)
        {
            EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(color));
        }
    }
}
#endif
