#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal sealed class NeoConsoleLogEntry
    {
        public NeoConsoleLogEntry(int id, string message, string stackTrace, LogType type, double timeSinceStartup)
        {
            Id = id;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
            TimeSinceStartup = timeSinceStartup;
        }

        public int Id { get; private set; }
        public string Message { get; private set; }
        public string StackTrace { get; private set; }
        public LogType Type { get; private set; }
        public double TimeSinceStartup { get; private set; }

        public bool HasSameContent(NeoConsoleLogEntry other)
        {
            return other != null &&
                   Type == other.Type &&
                   Message == other.Message &&
                   StackTrace == other.StackTrace;
        }
    }
}
#endif
