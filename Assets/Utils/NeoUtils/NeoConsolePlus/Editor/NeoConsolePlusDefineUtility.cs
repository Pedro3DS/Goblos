#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Neo.ConsolePlus.Editor
{
    internal static class NeoConsolePlusDefineUtility
    {
        public const string DisableRuntimeOverlaySymbol = "NEO_CONSOLEPLUS_DISABLE_RUNTIME_OVERLAY";

        public static bool IsRuntimeOverlayAutoCreationDisabled
        {
            get { return HasDefine(DisableRuntimeOverlaySymbol); }
            set { SetDefine(DisableRuntimeOverlaySymbol, value); }
        }

        public static BuildTargetGroup CurrentBuildTargetGroup
        {
            get
            {
                BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
                if (group == BuildTargetGroup.Unknown)
                    group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

                return group;
            }
        }

        public static string CurrentBuildTargetDisplayName
        {
            get { return CurrentBuildTargetGroup.ToString(); }
        }

        public static bool HasDefine(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            string[] symbols = GetDefines();
            for (int i = 0; i < symbols.Length; i++)
            {
                if (string.Equals(symbols[i], symbol, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static void SetDefine(string symbol, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            string normalizedSymbol = symbol.Trim();
            string[] currentSymbols = GetDefines();
            List<string> nextSymbols = new List<string>(currentSymbols.Length + 1);
            bool found = false;

            for (int i = 0; i < currentSymbols.Length; i++)
            {
                string current = currentSymbols[i];
                if (string.Equals(current, normalizedSymbol, StringComparison.Ordinal))
                {
                    found = true;
                    if (enabled)
                        nextSymbols.Add(current);

                    continue;
                }

                nextSymbols.Add(current);
            }

            if (enabled && !found)
                nextSymbols.Add(normalizedSymbol);

            string currentRaw = GetDefineString();
            string nextRaw = string.Join(";", nextSymbols.ToArray());
            if (string.Equals(currentRaw, nextRaw, StringComparison.Ordinal))
                return;

            PlayerSettings.SetScriptingDefineSymbols(GetCurrentNamedBuildTarget(), nextRaw);
            AssetDatabase.SaveAssets();
            Debug.Log("[NeoConsolePlus] Updated scripting define symbols for " + CurrentBuildTargetDisplayName + ".");
        }

        public static string GetDefineString()
        {
            return PlayerSettings.GetScriptingDefineSymbols(GetCurrentNamedBuildTarget());
        }

        private static string[] GetDefines()
        {
            string raw = GetDefineString();
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            string[] split = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> symbols = new List<string>(split.Length);
            for (int i = 0; i < split.Length; i++)
            {
                string value = split[i].Trim();
                if (!string.IsNullOrEmpty(value) && !symbols.Contains(value))
                    symbols.Add(value);
            }

            return symbols.ToArray();
        }

        private static NamedBuildTarget GetCurrentNamedBuildTarget()
        {
            return NamedBuildTarget.FromBuildTargetGroup(CurrentBuildTargetGroup);
        }
    }
}
#endif
