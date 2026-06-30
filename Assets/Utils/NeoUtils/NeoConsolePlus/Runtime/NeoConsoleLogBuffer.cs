#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoConsoleLogBuffer
    {
        private const int DefaultMaxEntries = 500;
        private const int MinimumMaxEntries = 50;

        private static readonly object SyncRoot = new object();
        private static readonly HashSet<object> NormalLogCaptureOwners = new HashSet<object>();

        private static NeoConsoleLogEntry[] entries = new NeoConsoleLogEntry[DefaultMaxEntries];
        private static int startIndex;
        private static int count;
        private static int nextId;
        private static int version;
        private static bool isListening;
        private static int maxEntries = DefaultMaxEntries;

        public static event Action<NeoConsoleLogEntry> EntryAdded;
        public static event Action Cleared;

        public static int Version
        {
            get
            {
                lock (SyncRoot)
                    return version;
            }
        }

        public static int Count
        {
            get
            {
                lock (SyncRoot)
                    return count;
            }
        }

        public static int MaxEntries
        {
            get { return maxEntries; }
            set
            {
                int safeValue = Mathf.Max(MinimumMaxEntries, value);
                lock (SyncRoot)
                {
                    if (safeValue == maxEntries)
                        return;

                    maxEntries = safeValue;
                    ResizeLocked(maxEntries);
                    version++;
                }
            }
        }

        public static void EnsureListening()
        {
            Application.logMessageReceived -= HandleLogReceived;
            Application.logMessageReceived += HandleLogReceived;
            isListening = true;
        }

        public static void StopListening()
        {
            if (!isListening)
                return;

            Application.logMessageReceived -= HandleLogReceived;
            isListening = false;
        }

        public static void SetNormalLogCaptureOwner(object owner, bool capture)
        {
            if (owner == null)
                return;

            lock (SyncRoot)
            {
                if (capture)
                    NormalLogCaptureOwners.Add(owner);
                else
                    NormalLogCaptureOwners.Remove(owner);
            }
        }

        public static void Clear()
        {
            Clear(false);
        }

        public static void Clear(bool includeCompilerErrors)
        {
            lock (SyncRoot)
            {
                if (includeCompilerErrors)
                    ClearEntriesLocked();
                else
                    KeepOnlyCompilerErrorsLocked();

                version++;
            }

            ClearUnityEditorConsole();

            Action cleared = Cleared;
            if (cleared != null)
                cleared();
        }

        private static void ClearUnityEditorConsole()
        {
#if UNITY_EDITOR
            try
            {
                Type logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null)
                    logEntriesType = Type.GetType("UnityEditorInternal.LogEntries,UnityEditor");

                if (logEntriesType == null)
                    return;

                MethodInfo clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (clearMethod != null)
                    clearMethod.Invoke(null, null);
            }
            catch
            {
                // The Unity Console clear API is editor-internal and can change between Unity versions.
                // NeoConsolePlus still clears its own log buffer even if the Unity Console cannot be cleared.
            }
#endif
        }

        public static void ClearCompilerErrors()
        {
            lock (SyncRoot)
            {
                RemoveWhereLocked(IsCompilerErrorEntry);
                version++;
            }

            Action cleared = Cleared;
            if (cleared != null)
                cleared();
        }

        public static void AddDirect(string message, string stackTrace, LogType type)
        {
            Add(message, stackTrace, type, true);
        }

        public static NeoConsoleLogEntry[] Snapshot()
        {
            lock (SyncRoot)
            {
                NeoConsoleLogEntry[] snapshot = new NeoConsoleLogEntry[count];
                CopyEntriesLocked(snapshot, 0);
                return snapshot;
            }
        }

        public static void CopySnapshot(List<NeoConsoleLogEntry> target)
        {
            if (target == null)
                return;

            lock (SyncRoot)
            {
                target.Clear();
                if (count == 0)
                    return;

                if (target.Capacity < count)
                    target.Capacity = count;

                for (int i = 0; i < count; i++)
                    target.Add(GetEntryAtLocked(i));
            }
        }

        private static void HandleLogReceived(string condition, string stackTrace, LogType type)
        {
            Add(condition, stackTrace, type, false);
        }

        private static void Add(string message, string stackTrace, LogType type, bool forceCapture)
        {
            if (!forceCapture && !ShouldCaptureLogType(type))
                return;

            NeoConsoleLogEntry entry = new NeoConsoleLogEntry(++nextId, message, stackTrace, type, (double)Time.realtimeSinceStartup);

            lock (SyncRoot)
            {
                AddLocked(entry);
                version++;
            }

            Action<NeoConsoleLogEntry> added = EntryAdded;
            if (added != null)
                added(entry);
        }

        private static bool ShouldCaptureLogType(LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                return true;

            lock (SyncRoot)
                return NormalLogCaptureOwners.Count > 0;
        }

        private static void AddLocked(NeoConsoleLogEntry entry)
        {
            if (entries == null || entries.Length != maxEntries)
                ResizeLocked(maxEntries);

            if (count < entries.Length)
            {
                entries[(startIndex + count) % entries.Length] = entry;
                count++;
                return;
            }

            entries[startIndex] = entry;
            startIndex = (startIndex + 1) % entries.Length;
        }

        private static void ResizeLocked(int newSize)
        {
            newSize = Mathf.Max(MinimumMaxEntries, newSize);
            NeoConsoleLogEntry[] newEntries = new NeoConsoleLogEntry[newSize];
            int newCount = Mathf.Min(count, newSize);
            int skip = count - newCount;

            for (int i = 0; i < newCount; i++)
                newEntries[i] = GetEntryAtLocked(skip + i);

            entries = newEntries;
            startIndex = 0;
            count = newCount;
        }

        private static void KeepOnlyCompilerErrorsLocked()
        {
            if (count == 0)
                return;

            NeoConsoleLogEntry[] kept = new NeoConsoleLogEntry[maxEntries];
            int keptCount = 0;
            for (int i = 0; i < count; i++)
            {
                NeoConsoleLogEntry entry = GetEntryAtLocked(i);
                if (!IsCompilerErrorEntry(entry))
                    continue;

                if (keptCount >= kept.Length)
                    break;

                kept[keptCount++] = entry;
            }

            entries = kept;
            startIndex = 0;
            count = keptCount;
        }

        private static void RemoveWhereLocked(Func<NeoConsoleLogEntry, bool> predicate)
        {
            if (predicate == null || count == 0)
                return;

            NeoConsoleLogEntry[] kept = new NeoConsoleLogEntry[maxEntries];
            int keptCount = 0;
            for (int i = 0; i < count; i++)
            {
                NeoConsoleLogEntry entry = GetEntryAtLocked(i);
                if (predicate(entry))
                    continue;

                kept[keptCount++] = entry;
            }

            entries = kept;
            startIndex = 0;
            count = keptCount;
        }

        private static void ClearEntriesLocked()
        {
            Array.Clear(entries, 0, entries.Length);
            startIndex = 0;
            count = 0;
        }

        private static void CopyEntriesLocked(NeoConsoleLogEntry[] target, int targetIndex)
        {
            if (target == null)
                return;

            for (int i = 0; i < count && targetIndex + i < target.Length; i++)
                target[targetIndex + i] = GetEntryAtLocked(i);
        }

        private static NeoConsoleLogEntry GetEntryAtLocked(int index)
        {
            if (entries == null || entries.Length == 0 || index < 0 || index >= count)
                return null;

            return entries[(startIndex + index) % entries.Length];
        }

        public static bool IsCompilerErrorEntry(NeoConsoleLogEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.Type != LogType.Error && entry.Type != LogType.Assert && entry.Type != LogType.Warning)
                return false;

            string message = entry.Message ?? string.Empty;
            if (string.IsNullOrEmpty(message))
                return false;

            // Unity compiler diagnostics are usually reported as file-path messages, for example:
            // Assets/Scripts/Foo.cs(10,20): error CS1002: ; expected
            // Assets/Scripts/Foo.cs(10,20): warning CS0618: 'Object.GetInstanceID()' is obsolete...
            // Clear in Unity's native Console does not remove active script diagnostics while they still exist.
            bool hasCSharpCompilerCode = message.IndexOf("error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         message.IndexOf("warning CS", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasCompilerLocation = message.IndexOf(".cs(", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       message.IndexOf(".asmdef", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       message.IndexOf(".rsp", StringComparison.OrdinalIgnoreCase) >= 0;
            bool looksLikeUnityAssetPath = message.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           message.IndexOf("Packages/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           message.IndexOf("Library/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0;

            return hasCSharpCompilerCode && (hasCompilerLocation || looksLikeUnityAssetPath);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInitialize()
        {
            EnsureListening();
        }
    }
}
#endif
