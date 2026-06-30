#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Neo.Debugging.Editor
{
    internal sealed class NeoDebugBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0)
                NeoDebugEditorSettingsUtility.WriteDevelopmentBuildRuntimeSettings();
            else
                NeoDebugEditorSettingsUtility.DeleteRuntimeSettingsAsset();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            NeoDebugEditorSettingsUtility.DeleteRuntimeSettingsAsset();
        }
    }
}
#endif
