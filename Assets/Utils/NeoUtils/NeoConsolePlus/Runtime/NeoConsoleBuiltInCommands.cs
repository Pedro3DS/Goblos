#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleBuiltInCommands
    {
        internal const string PackageVersion = "1.2.1";

        [NeoCommand("help", "Shows basic NeoConsolePlus help and built-in command groups available in the current console.")]
        private static string Help()
        {
            NeoCommandExecutionContext context = NeoCommandRegistry.CurrentExecutionContext;
            StringBuilder builder = new StringBuilder(768);
            builder.AppendLine("NeoConsolePlus help");
            builder.AppendLine("Context: " + context);
            builder.AppendLine("Commands start with '/'. Example: /neo.scene.current");
            builder.AppendLine();

            AppendGroup(builder, context, "Core", "help", "clear", "version");
            AppendGroup(builder, context, "Time", "neo.time.info", "neo.time.scale", "neo.time.pause", "neo.time.resume", "neo.time.fixed_delta");
            AppendGroup(builder, context, "Scene", "neo.scene.current", "neo.scene.list", "neo.scene.reload", "neo.scene.load", "neo.scene.load_index");
            AppendGroup(builder, context, "FPS", "neo.fps.show", "neo.fps.hide", "neo.fps.toggle", "neo.fps.info");

            return builder.ToString().TrimEnd();
        }

        [NeoCommand("clear", "Clears NeoConsolePlus captured logs.")]
        private static string Clear()
        {
            NeoConsoleLogBuffer.Clear();
            return "NeoConsolePlus logs cleared.";
        }

        [NeoCommand("version", "Shows the NeoConsolePlus package version.")]
        private static string Version()
        {
            return "NeoConsolePlus " + PackageVersion + " | Unity " + Application.unityVersion + " | " + Application.platform;
        }

        private static void AppendGroup(StringBuilder builder, NeoCommandExecutionContext context, string title, params string[] commandNames)
        {
            bool wroteHeader = false;
            for (int i = 0; i < commandNames.Length; i++)
            {
                NeoCommandInfo command;
                if (!NeoCommandRegistry.TryGetCommand(commandNames[i], context, out command))
                    continue;

                if (!wroteHeader)
                {
                    builder.AppendLine(title + ":");
                    wroteHeader = true;
                }

                builder.Append("  ").Append(command.Signature);
                if (!string.IsNullOrEmpty(command.Description))
                    builder.Append(" - ").Append(command.Description);
                builder.AppendLine();
            }

            if (wroteHeader)
                builder.AppendLine();
        }
    }
}
#endif
