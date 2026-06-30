#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Neo.ConsolePlus
{
    internal struct NeoConsoleBuildSceneInfo
    {
        public NeoConsoleBuildSceneInfo(string name, string path, int buildIndex, bool enabled)
        {
            Name = name ?? string.Empty;
            Path = path ?? string.Empty;
            BuildIndex = buildIndex;
            Enabled = enabled;
        }

        public string Name;
        public string Path;
        public int BuildIndex;
        public bool Enabled;
    }

    internal static class NeoConsoleSceneUtility
    {
        public static NeoConsoleBuildSceneInfo[] GetBuildScenes()
        {
#if UNITY_EDITOR
            EditorBuildSettingsScene[] editorScenes = EditorBuildSettings.scenes;
            List<NeoConsoleBuildSceneInfo> result = new List<NeoConsoleBuildSceneInfo>();
            int enabledBuildIndex = 0;

            if (editorScenes != null)
            {
                for (int i = 0; i < editorScenes.Length; i++)
                {
                    EditorBuildSettingsScene scene = editorScenes[i];
                    if (scene == null || string.IsNullOrEmpty(scene.path))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(scene.path);
                    int buildIndex = scene.enabled ? enabledBuildIndex : -1;
                    if (scene.enabled)
                        enabledBuildIndex++;

                    result.Add(new NeoConsoleBuildSceneInfo(name, scene.path, buildIndex, scene.enabled));
                }
            }

            return result.ToArray();
#else
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            NeoConsoleBuildSceneInfo[] result = new NeoConsoleBuildSceneInfo[sceneCount];
            for (int i = 0; i < sceneCount; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                result[i] = new NeoConsoleBuildSceneInfo(name, path, i, true);
            }

            return result;
#endif
        }

        public static bool TryFindBuildScene(string sceneNameOrPath, bool requireEnabled, out NeoConsoleBuildSceneInfo scene)
        {
            scene = default(NeoConsoleBuildSceneInfo);
            string query = (sceneNameOrPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(query))
                return false;

            query = TrimQuotes(query);
            NeoConsoleBuildSceneInfo[] scenes = GetBuildScenes();

            for (int i = 0; i < scenes.Length; i++)
            {
                NeoConsoleBuildSceneInfo candidate = scenes[i];
                if (requireEnabled && !candidate.Enabled)
                    continue;

                if (string.Equals(candidate.Name, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Path, query, StringComparison.OrdinalIgnoreCase))
                {
                    scene = candidate;
                    return true;
                }
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                NeoConsoleBuildSceneInfo candidate = scenes[i];
                if (requireEnabled && !candidate.Enabled)
                    continue;

                string fileName = Path.GetFileName(candidate.Path);
                if (string.Equals(fileName, query, StringComparison.OrdinalIgnoreCase))
                {
                    scene = candidate;
                    return true;
                }
            }

            return false;
        }

        public static string QuoteSceneNameForCommand(string sceneName)
        {
            string value = sceneName ?? string.Empty;
            if (!NeedsQuotes(value))
                return value;

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool NeedsQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static string TrimQuotes(string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Length >= 2)
            {
                char first = safe[0];
                char last = safe[safe.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    return safe.Substring(1, safe.Length - 2);
            }

            return safe;
        }
    }
}
#endif
