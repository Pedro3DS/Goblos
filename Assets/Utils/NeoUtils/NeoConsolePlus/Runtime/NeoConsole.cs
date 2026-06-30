using System.Diagnostics;

namespace Neo.ConsolePlus
{
    /// <summary>
    /// Public facade for optional NeoConsolePlus runtime/editor actions.
    ///
    /// The command system itself is intended to be used through the NeoCommand attribute.
    /// These methods are safe to call from gameplay code: in regular release builds they
    /// either return false or compile away.
    /// </summary>
    public static class NeoConsole
    {
        /// <summary>
        /// Executes a registered NeoConsolePlus command. Returns false in regular release builds.
        /// </summary>
        public static bool Execute(string command)
        {
            string message;
            return Execute(command, out message);
        }

        /// <summary>
        /// Executes a registered NeoConsolePlus command and returns the execution message.
        /// Returns false in regular release builds.
        /// </summary>
        public static bool Execute(string command, out string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoCommandResult result = NeoCommandRegistry.Execute(command, NeoCommandExecutionContext.Runtime);
            message = result.Message;
            return result.Success;
#else
            message = string.Empty;
            return false;
#endif
        }

        /// <summary>
        /// Refreshes registered commands in the Editor and Development Builds.
        /// This call is removed from regular release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void RefreshCommands()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoCommandRegistry.Refresh();
#endif
        }

        /// <summary>
        /// Clears NeoConsolePlus captured logs in the Editor and Development Builds.
        /// In the Unity Editor, this also attempts to clear the standard Unity Console window.
        /// This call is removed from regular release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ClearLogs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoConsoleLogBuffer.Clear();
#endif
        }

        /// <summary>
        /// Shows the runtime overlay in Play Mode or Development Builds.
        /// This call is removed from regular release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ShowRuntimeOverlay()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoRuntimeOverlay.Show();
#endif
        }

        /// <summary>
        /// Hides the runtime overlay in Play Mode or Development Builds.
        /// This call is removed from regular release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void HideRuntimeOverlay()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoRuntimeOverlay.Hide();
#endif
        }

        /// <summary>
        /// Toggles the runtime overlay in Play Mode or Development Builds.
        /// This call is removed from regular release builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void ToggleRuntimeOverlay()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NeoRuntimeOverlay.Toggle();
#endif
        }
    }
}
