#if UNITY_EDITOR
using Neo.ConsolePlus;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;

namespace Neo.ConsolePlus.Editor
{
    [InitializeOnLoad]
    internal static class NeoConsolePlusEditorInitializer
    {
        static NeoConsolePlusEditorInitializer()
        {
            NeoConsolePlusEditorSettings.EnsureDefaultsInitialized();
            ApplyEditorSettings();
            NeoConsoleLogBuffer.EnsureListening();
            AssemblyReloadEvents.beforeAssemblyReload += ClearBeforeAssemblyReloadIfNeeded;
            EditorApplication.delayCall += RefreshAfterReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            CompilationPipeline.compilationStarted += HandleCompilationStarted;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorSceneManager.sceneClosed += HandleSceneClosed;
        }

        private static void ApplyEditorSettings()
        {
            NeoConsoleLogBuffer.MaxEntries = NeoConsolePlusEditorSettings.MaxLogEntries;
            NeoRuntimeOverlay.OpenOnError = NeoConsolePlusEditorSettings.OpenRuntimeOverlayOnError;
        }

        private static void HandleHierarchyChanged()
        {
            NeoCommandTargetCache.MarkDirty();
        }

        private static void HandleSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            NeoCommandTargetCache.MarkDirty();
        }

        private static void HandleSceneClosed(UnityEngine.SceneManagement.Scene scene)
        {
            NeoCommandTargetCache.MarkDirty();
        }

        private static void HandleCompilationStarted(object context)
        {
            // Script compiler diagnostics are sticky like Unity Console diagnostics: manual Clear keeps them,
            // but a new compilation pass can remove stale resolved errors and let current diagnostics be reported again.
            NeoConsoleLogBuffer.ClearCompilerErrors();
        }

        private static void RefreshAfterReload()
        {
            // Keep reload fast. Command registration is lazy and will run only when the
            // console, autocomplete, or Execute API actually needs the registry.
            ApplyEditorSettings();
            NeoConsoleLogBuffer.EnsureListening();
            NeoCommandRegistry.MarkDirty();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && NeoConsolePlusEditorSettings.ClearOnPlay)
                NeoConsoleLogBuffer.Clear();

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                NeoRuntimeOverlay.ForceDestroyEditorInstances();

            ApplyEditorSettings();
            NeoConsoleLogBuffer.EnsureListening();
            NeoCommandRegistry.MarkDirty();
        }

        private static void ClearBeforeAssemblyReloadIfNeeded()
        {
            if (NeoConsolePlusEditorSettings.ClearOnRecompile)
                NeoConsoleLogBuffer.Clear();
        }
    }

    internal sealed class NeoConsolePlusBuildClearProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return int.MinValue + 1000; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (NeoConsolePlusEditorSettings.ClearOnBuild)
                NeoConsoleLogBuffer.Clear();
        }
    }
}
#endif
