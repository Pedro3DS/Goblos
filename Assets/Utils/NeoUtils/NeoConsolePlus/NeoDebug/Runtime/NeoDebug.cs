using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Neo.Debugging
{
    /// <summary>
    /// Lightweight configurable wrapper around UnityEngine.Debug.
    ///
    /// Calls are compiled only for the Editor and Development Builds.
    /// In regular release builds, the C# compiler removes NeoDebug calls entirely,
    /// including argument evaluation.
    /// </summary>
    public static class NeoDebug
    {
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Log(object message, [CallerFilePath] string callerFilePath = "")
        {
            if (!NeoDebugSettings.CanLog())
                return;

            UnityEngine.Debug.Log(NeoDebugFormatter.Format(message, NeoDebugType.Log, callerFilePath));
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(object message, [CallerFilePath] string callerFilePath = "")
        {
            if (!NeoDebugSettings.CanLog())
                return;

            UnityEngine.Debug.LogWarning(NeoDebugFormatter.Format(message, NeoDebugType.Warning, callerFilePath));
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Error(object message, [CallerFilePath] string callerFilePath = "")
        {
            if (!NeoDebugSettings.CanLog())
                return;

            UnityEngine.Debug.LogError(NeoDebugFormatter.Format(message, NeoDebugType.Error, callerFilePath));

#if UNITY_EDITOR
            if (NeoDebugSettings.PauseOnError)
                UnityEngine.Debug.Break();
#endif
        }
    }
}
