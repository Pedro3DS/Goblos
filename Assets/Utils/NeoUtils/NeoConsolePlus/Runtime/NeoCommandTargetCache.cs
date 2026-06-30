#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoCommandTargetCache
    {
        private const float PlayModeRefreshIntervalSeconds = 1.0f;

        private static readonly Dictionary<Type, CacheEntry> Cache = new Dictionary<Type, CacheEntry>();
        private static int version;

        public static int Version
        {
            get { return version; }
        }

        public static MonoBehaviour[] FindActiveTargets(Type targetType)
        {
            if (targetType == null || !typeof(MonoBehaviour).IsAssignableFrom(targetType))
                return new MonoBehaviour[0];

            CacheEntry entry;
            float now = Time.realtimeSinceStartup;
            bool hasEntry = Cache.TryGetValue(targetType, out entry);
            bool shouldRefresh = !hasEntry || ContainsInvalidTarget(entry.Targets);

            if (!shouldRefresh && Application.isPlaying)
                shouldRefresh = now - entry.LastRefreshTime >= PlayModeRefreshIntervalSeconds;

            if (shouldRefresh)
            {
                entry = Refresh(targetType, now);
                Cache[targetType] = entry;
            }

            return entry.Targets;
        }

        public static void Clear()
        {
            if (Cache.Count == 0)
                return;

            Cache.Clear();
            version++;
        }

        public static void MarkDirty()
        {
            Clear();
        }

        private static bool ContainsInvalidTarget(MonoBehaviour[] targets)
        {
            if (targets == null)
                return true;

            for (int i = 0; i < targets.Length; i++)
            {
                MonoBehaviour target = targets[i];
                if (target == null || !target.isActiveAndEnabled)
                    return true;
            }

            return false;
        }

        private static CacheEntry Refresh(Type targetType, float time)
        {
            UnityEngine.Object[] found = NeoUnityObjectUtility.FindObjectsByType(targetType);
            List<MonoBehaviour> targets = new List<MonoBehaviour>(found.Length);

            for (int i = 0; i < found.Length; i++)
            {
                MonoBehaviour target = found[i] as MonoBehaviour;
                if (target == null || !target.isActiveAndEnabled)
                    continue;

                targets.Add(target);
            }

            targets.Sort(CompareTargets);
            version++;
            return new CacheEntry(targets.ToArray(), time);
        }

        private static int CompareTargets(MonoBehaviour left, MonoBehaviour right)
        {
            return string.Compare(GetStableDisplayName(left), GetStableDisplayName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetStableDisplayName(MonoBehaviour target)
        {
            if (target == null)
                return string.Empty;

            if (target.gameObject != null)
                return target.gameObject.name + "|" + target.GetType().Name;

            return target.name + "|" + target.GetType().Name;
        }

        private struct CacheEntry
        {
            public CacheEntry(MonoBehaviour[] targets, float lastRefreshTime)
            {
                Targets = targets ?? new MonoBehaviour[0];
                LastRefreshTime = lastRefreshTime;
            }

            public readonly MonoBehaviour[] Targets;
            public readonly float LastRefreshTime;
        }
    }
}
#endif
