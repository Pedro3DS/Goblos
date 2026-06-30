#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleSceneBuiltInCommands
    {
        [NeoCommand("neo.scene.current", "Shows the active scene.")]
        private static string Current()
        {
            Scene scene = SceneManager.GetActiveScene();
            return "Active scene: " + GetSceneDisplayName(scene);
        }

        [NeoCommand("neo.scene.list", "Lists scenes registered in Build Settings.")]
        private static string List()
        {
            NeoConsoleBuildSceneInfo[] scenes = NeoConsoleSceneUtility.GetBuildScenes();
            if (scenes == null || scenes.Length == 0)
                return "No scenes found in Build Settings.";

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Build Settings scenes:");
            for (int i = 0; i < scenes.Length; i++)
            {
                NeoConsoleBuildSceneInfo scene = scenes[i];
                builder.Append("  ");
                builder.Append(scene.Enabled ? scene.BuildIndex.ToString(CultureInfo.InvariantCulture) : "-");
                builder.Append(" | ");
                builder.Append(scene.Name);
                if (!scene.Enabled)
                    builder.Append(" (disabled)");
                if (!string.IsNullOrEmpty(scene.Path))
                    builder.Append(" | ").Append(scene.Path);
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        [NeoCommand("neo.scene.reload", "Reloads the active scene.")]
        private static string Reload()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return "No valid active scene found.";

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (string.IsNullOrEmpty(scene.path))
                    return "The active scene has no asset path and cannot be reloaded in Edit Mode.";

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return "Scene reload cancelled.";

                EditorSceneManager.OpenScene(scene.path);
                return "Reloaded scene: " + scene.path;
            }
#endif

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return "Reloading scene: " + GetSceneDisplayName(scene);
            }

            SceneManager.LoadScene(scene.name);
            return "Reloading scene: " + scene.name;
        }

        [NeoCommand("neo.scene.load", "Loads a scene by Build Settings name or path.")]
        private static string Load(string sceneName)
        {
            bool requireEnabled = true;
#if UNITY_EDITOR
            requireEnabled = Application.isPlaying;
#endif

            NeoConsoleBuildSceneInfo scene;
            if (!NeoConsoleSceneUtility.TryFindBuildScene(sceneName, requireEnabled, out scene))
            {
                return requireEnabled
                    ? "Scene not found or not enabled in Build Settings: " + sceneName
                    : "Scene not found in Build Settings: " + sceneName;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return "Scene load cancelled.";

                EditorSceneManager.OpenScene(scene.Path);
                return "Opened scene: " + scene.Path;
            }
#endif

            if (scene.BuildIndex >= 0)
            {
                SceneManager.LoadScene(scene.BuildIndex);
                return "Loading scene: " + scene.Name;
            }

            SceneManager.LoadScene(scene.Name);
            return "Loading scene: " + scene.Name;
        }

        [NeoCommand("neo.scene.load_index", "Loads a scene by Build Settings build index.")]
        private static string LoadIndex(int buildIndex)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
                return "Invalid build index: " + buildIndex.ToString(CultureInfo.InvariantCulture) + ". Scene count: " + SceneManager.sceneCountInBuildSettings.ToString(CultureInfo.InvariantCulture) + ".";

            string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (string.IsNullOrEmpty(path))
                return "No scene path found for build index: " + buildIndex.ToString(CultureInfo.InvariantCulture) + ".";

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return "Scene load cancelled.";

                EditorSceneManager.OpenScene(path);
                return "Opened scene: " + path;
            }
#endif

            SceneManager.LoadScene(buildIndex);
            return "Loading scene index " + buildIndex.ToString(CultureInfo.InvariantCulture) + ": " + path;
        }

        private static string GetSceneDisplayName(Scene scene)
        {
            if (!scene.IsValid())
                return "<invalid>";

            string name = string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name;
            string index = scene.buildIndex >= 0 ? scene.buildIndex.ToString(CultureInfo.InvariantCulture) : "not in build";
            return name + " | buildIndex: " + index + " | path: " + scene.path;
        }
    }
}
#endif
