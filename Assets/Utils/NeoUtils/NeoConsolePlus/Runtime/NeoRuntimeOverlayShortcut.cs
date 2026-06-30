#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Neo.ConsolePlus
{
    internal static class NeoRuntimeOverlayShortcut
    {
        private const KeyCode DefaultPrimaryKey = KeyCode.BackQuote;
        private const KeyCode DefaultSecondaryKey = KeyCode.F1;

#if UNITY_EDITOR
        private const string PrimaryEditorPrefKey = "Neo.ConsolePlus.RuntimeOverlay.PrimaryKeyV2";
        private const string SecondaryEditorPrefKey = "Neo.ConsolePlus.RuntimeOverlay.SecondaryKeyV2";
#endif

        private static bool keysLoaded;
        private static KeyCode cachedPrimaryKey;
        private static KeyCode cachedSecondaryKey;

        public static KeyCode PrimaryKey
        {
            get
            {
                EnsureKeysLoaded();
                return cachedPrimaryKey;
            }
        }

        public static KeyCode SecondaryKey
        {
            get
            {
                EnsureKeysLoaded();
                return cachedSecondaryKey;
            }
        }

        public static string DisplayText
        {
            get { return FormatKey(PrimaryKey) + " or " + FormatKey(SecondaryKey); }
        }

        private static string PrimaryEditorPrefKeySafe
        {
            get
            {
#if UNITY_EDITOR
                return PrimaryEditorPrefKey;
#else
                return string.Empty;
#endif
            }
        }

        private static string SecondaryEditorPrefKeySafe
        {
            get
            {
#if UNITY_EDITOR
                return SecondaryEditorPrefKey;
#else
                return string.Empty;
#endif
            }
        }

        public static void SetPrimaryKey(KeyCode key)
        {
            SaveKey(PrimaryEditorPrefKeySafe, key);
            cachedPrimaryKey = key;
            keysLoaded = true;
        }

        public static void SetSecondaryKey(KeyCode key)
        {
            SaveKey(SecondaryEditorPrefKeySafe, key);
            cachedSecondaryKey = key;
            keysLoaded = true;
        }

        public static void ResetToDefaults()
        {
            SetPrimaryKey(DefaultPrimaryKey);
            SetSecondaryKey(DefaultSecondaryKey);
        }

        public static bool IsTogglePressed()
        {
            KeyCode primary = PrimaryKey;
            KeyCode secondary = SecondaryKey;

            return IsKeyPressed(primary) || (secondary != primary && IsKeyPressed(secondary));
        }

        public static string FormatKey(KeyCode key)
        {
            if (key == KeyCode.Quote)
                return "Quote (')";

            if (key == KeyCode.BackQuote)
                return "Backquote (`)";

            return key.ToString();
        }

        private static void EnsureKeysLoaded()
        {
            if (keysLoaded)
                return;

            cachedPrimaryKey = LoadKey(PrimaryEditorPrefKeySafe, DefaultPrimaryKey);
            cachedSecondaryKey = LoadKey(SecondaryEditorPrefKeySafe, DefaultSecondaryKey);
            keysLoaded = true;
        }

        private static KeyCode LoadKey(string prefKey, KeyCode fallback)
        {
#if UNITY_EDITOR
            string savedValue = EditorPrefs.GetString(prefKey, fallback.ToString());
            KeyCode parsed;
            if (Enum.TryParse(savedValue, true, out parsed))
                return parsed;
#endif
            return fallback;
        }

        private static void SaveKey(string prefKey, KeyCode key)
        {
#if UNITY_EDITOR
            EditorPrefs.SetString(prefKey, key.ToString());
#endif
        }

        private static bool IsKeyPressed(KeyCode key)
        {
            if (key == KeyCode.None)
                return false;

#if ENABLE_INPUT_SYSTEM
            if (NeoInputSystemReflection.IsKeyPressed(key))
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static class NeoInputSystemReflection
        {
            private static bool initialized;
            private static Type keyboardType;
            private static Type keyType;
            private static PropertyInfo currentProperty;
            private static PropertyInfo itemProperty;
            private static PropertyInfo wasPressedThisFrameProperty;
            private static readonly System.Collections.Generic.Dictionary<KeyCode, object[]> ParsedKeyCache = new System.Collections.Generic.Dictionary<KeyCode, object[]>();

            public static bool IsKeyPressed(KeyCode keyCode)
            {
                if (!EnsureInitialized())
                    return false;

                object keyboard = currentProperty.GetValue(null, null);
                if (keyboard == null)
                    return false;

                object[] inputKeys = GetParsedInputSystemKeys(keyCode);
                for (int i = 0; i < inputKeys.Length; i++)
                {
                    object inputKey = inputKeys[i];
                    if (inputKey == null)
                        continue;

                    object keyControl = itemProperty.GetValue(keyboard, new[] { inputKey });
                    if (keyControl == null)
                        continue;

                    object pressed = wasPressedThisFrameProperty.GetValue(keyControl, null);
                    if (pressed is bool && (bool)pressed)
                        return true;
                }

                return false;
            }

            private static bool EnsureInitialized()
            {
                if (initialized)
                    return keyboardType != null && keyType != null && currentProperty != null && itemProperty != null && wasPressedThisFrameProperty != null;

                initialized = true;

                keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                keyType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");

                if (keyboardType == null || keyType == null)
                    return false;

                currentProperty = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                itemProperty = keyboardType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, null, new[] { keyType }, null);

                Type buttonControlType = Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem");
                if (buttonControlType != null)
                    wasPressedThisFrameProperty = buttonControlType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);

                return currentProperty != null && itemProperty != null && wasPressedThisFrameProperty != null;
            }


            private static object[] GetParsedInputSystemKeys(KeyCode keyCode)
            {
                object[] cached;
                if (ParsedKeyCache.TryGetValue(keyCode, out cached))
                    return cached;

                string[] keyNames = GetInputSystemKeyNames(keyCode);
                System.Collections.Generic.List<object> parsedKeys = new System.Collections.Generic.List<object>(keyNames.Length);
                for (int i = 0; i < keyNames.Length; i++)
                {
                    object inputKey;
                    if (TryParseInputSystemKey(keyNames[i], out inputKey) && inputKey != null)
                        parsedKeys.Add(inputKey);
                }

                cached = parsedKeys.ToArray();
                ParsedKeyCache[keyCode] = cached;
                return cached;
            }

            private static bool TryParseInputSystemKey(string keyName, out object inputKey)
            {
                inputKey = null;
                if (string.IsNullOrEmpty(keyName) || keyType == null)
                    return false;

                try
                {
                    inputKey = Enum.Parse(keyType, keyName, true);
                    return inputKey != null;
                }
                catch
                {
                    return false;
                }
            }

            private static string[] GetInputSystemKeyNames(KeyCode keyCode)
            {
                switch (keyCode)
                {
                    case KeyCode.BackQuote:
                        return new[] { "Backquote", "BackQuote" };
                    case KeyCode.Quote:
                        return new[] { "Quote", "Apostrophe" };
                    case KeyCode.Alpha0:
                        return new[] { "Digit0", "D0" };
                    case KeyCode.Alpha1:
                        return new[] { "Digit1", "D1" };
                    case KeyCode.Alpha2:
                        return new[] { "Digit2", "D2" };
                    case KeyCode.Alpha3:
                        return new[] { "Digit3", "D3" };
                    case KeyCode.Alpha4:
                        return new[] { "Digit4", "D4" };
                    case KeyCode.Alpha5:
                        return new[] { "Digit5", "D5" };
                    case KeyCode.Alpha6:
                        return new[] { "Digit6", "D6" };
                    case KeyCode.Alpha7:
                        return new[] { "Digit7", "D7" };
                    case KeyCode.Alpha8:
                        return new[] { "Digit8", "D8" };
                    case KeyCode.Alpha9:
                        return new[] { "Digit9", "D9" };
                    case KeyCode.Keypad0:
                        return new[] { "Numpad0" };
                    case KeyCode.Keypad1:
                        return new[] { "Numpad1" };
                    case KeyCode.Keypad2:
                        return new[] { "Numpad2" };
                    case KeyCode.Keypad3:
                        return new[] { "Numpad3" };
                    case KeyCode.Keypad4:
                        return new[] { "Numpad4" };
                    case KeyCode.Keypad5:
                        return new[] { "Numpad5" };
                    case KeyCode.Keypad6:
                        return new[] { "Numpad6" };
                    case KeyCode.Keypad7:
                        return new[] { "Numpad7" };
                    case KeyCode.Keypad8:
                        return new[] { "Numpad8" };
                    case KeyCode.Keypad9:
                        return new[] { "Numpad9" };
                    case KeyCode.KeypadPeriod:
                        return new[] { "NumpadPeriod", "NumpadDecimal" };
                    case KeyCode.KeypadDivide:
                        return new[] { "NumpadDivide" };
                    case KeyCode.KeypadMultiply:
                        return new[] { "NumpadMultiply" };
                    case KeyCode.KeypadMinus:
                        return new[] { "NumpadMinus" };
                    case KeyCode.KeypadPlus:
                        return new[] { "NumpadPlus" };
                    case KeyCode.KeypadEnter:
                        return new[] { "NumpadEnter" };
                    case KeyCode.Return:
                        return new[] { "Enter", "Return" };
                    case KeyCode.Escape:
                        return new[] { "Escape", "Esc" };
                    case KeyCode.LeftControl:
                        return new[] { "LeftCtrl", "LeftControl" };
                    case KeyCode.RightControl:
                        return new[] { "RightCtrl", "RightControl" };
                    case KeyCode.LeftCommand:
                        return new[] { "LeftCommand", "LeftMeta" };
                    case KeyCode.RightCommand:
                        return new[] { "RightCommand", "RightMeta" };
                    case KeyCode.UpArrow:
                        return new[] { "UpArrow" };
                    case KeyCode.DownArrow:
                        return new[] { "DownArrow" };
                    case KeyCode.LeftArrow:
                        return new[] { "LeftArrow" };
                    case KeyCode.RightArrow:
                        return new[] { "RightArrow" };
                    case KeyCode.PageUp:
                        return new[] { "PageUp" };
                    case KeyCode.PageDown:
                        return new[] { "PageDown" };
                    case KeyCode.CapsLock:
                        return new[] { "CapsLock" };
                    case KeyCode.Numlock:
                        return new[] { "NumLock" };
                    case KeyCode.ScrollLock:
                        return new[] { "ScrollLock" };
                    default:
                        return new[] { keyCode.ToString() };
                }
            }
        }
#endif
    }
}
#endif
