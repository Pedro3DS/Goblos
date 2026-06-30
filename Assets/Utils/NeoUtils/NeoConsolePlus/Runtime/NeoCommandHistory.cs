#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Neo.ConsolePlus
{
    internal static class NeoCommandHistory
    {
        private const int MaxEntries = 10;
        private static readonly List<string> Entries = new List<string>(MaxEntries);

        public static int Count
        {
            get { return Entries.Count; }
        }

        public static void Add(string command)
        {
            string safeCommand = (command ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(safeCommand))
                return;

            if (Entries.Count > 0 && Entries[Entries.Count - 1] == safeCommand)
                return;

            Entries.Add(safeCommand);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        }

        public static string Get(int index)
        {
            if (index < 0 || index >= Entries.Count)
                return string.Empty;

            return Entries[index];
        }
    }
}
#endif
