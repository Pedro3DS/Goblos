using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Neo.Debugging
{
    internal static class NeoDebugFormatter
    {
        private const string NullMessage = "null";
        private const int MaxObjectDepth = 2;
        private const int MaxCollectionItems = 3;
        private static readonly Dictionary<string, string> CallerNameCache = new Dictionary<string, string>(64);

        public static string Format(object message, NeoDebugType type, string callerFilePath)
        {
            string body = FormatMessageBody(message);

            if (!NeoDebugSettings.ShowScriptPrefix)
                return body;

            string callerName = GetCallerName(callerFilePath);
            string prefix = "[" + callerName + "]";

#if UNITY_EDITOR
            if (NeoDebugSettings.ColorPrefix)
                prefix = Colorize(prefix, type);
#endif

            return prefix + " " + body;
        }

        private static string FormatMessageBody(object message)
        {
            if (message == null)
                return NullMessage;

            return FormatValue(message, 0, false);
        }

        private static string FormatValue(object value, int depth, bool quoteStrings)
        {
            if (value == null)
                return NullMessage;

            Type type = value.GetType();

            if (type == typeof(string))
                return quoteStrings ? QuoteString((string)value) : (string)value;

            if (type == typeof(char))
                return quoteStrings ? QuoteString(value.ToString()) : value.ToString();

            if (type.IsEnum)
                return value.ToString();

            if (IsPrimitiveLike(type))
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return value.ToString();

            if (TryFormatDictionary(value, type, depth, out string dictionaryText))
                return dictionaryText;

            if (TryFormatEnumerable(value, depth, out string enumerableText))
                return enumerableText;

            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
                return value.ToString();

            FieldInfo[] fields = GetSerializableFields(type);
            if (fields.Length == 0 || depth >= MaxObjectDepth)
                return value.ToString();

            List<string> parts = new List<string>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch
                {
                    continue;
                }

                parts.Add(field.Name + ": " + FormatValue(fieldValue, depth + 1, true));
            }

            if (parts.Count == 0)
                return value.ToString();

            return type.Name + ": " + string.Join(", ", parts.ToArray());
        }

        private static bool TryFormatDictionary(object value, Type type, int depth, out string formatted)
        {
            formatted = string.Empty;

            if (value == null || depth >= MaxObjectDepth)
                return false;

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                formatted = FormatDictionary(dictionary, depth);
                return true;
            }

            if (!IsGenericDictionaryLike(type))
                return false;

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
                return false;

            formatted = FormatKeyValueEnumerable(enumerable, type, depth);
            return true;
        }

        private static bool TryFormatEnumerable(object value, int depth, out string formatted)
        {
            formatted = string.Empty;

            if (value == null || depth >= MaxObjectDepth)
                return false;

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
                return false;

            formatted = FormatEnumerable(enumerable, value.GetType(), depth);
            return true;
        }

        private static string FormatDictionary(IDictionary dictionary, int depth)
        {
            int totalCount = dictionary.Count;
            List<string> parts = new List<string>(Math.Min(totalCount, MaxCollectionItems) + 1);
            int index = 0;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (index >= MaxCollectionItems)
                    break;

                string key = FormatValue(entry.Key, depth + 1, true);
                string itemValue = FormatValue(entry.Value, depth + 1, true);
                parts.Add(key + ": " + itemValue);
                index++;
            }

            AddTruncationSuffix(parts, totalCount, index);
            return "{" + string.Join(", ", parts.ToArray()) + "}";
        }

        private static string FormatKeyValueEnumerable(IEnumerable enumerable, Type type, int depth)
        {
            int totalCount = TryGetCount(type, enumerable);
            List<string> parts = new List<string>(MaxCollectionItems + 1);
            int index = 0;

            foreach (object entry in enumerable)
            {
                if (index >= MaxCollectionItems)
                    break;

                if (!TryReadKeyValue(entry, out object key, out object itemValue))
                    break;

                parts.Add(FormatValue(key, depth + 1, true) + ": " + FormatValue(itemValue, depth + 1, true));
                index++;
            }

            AddTruncationSuffix(parts, totalCount, index);
            return "{" + string.Join(", ", parts.ToArray()) + "}";
        }

        private static string FormatEnumerable(IEnumerable enumerable, Type type, int depth)
        {
            int totalCount = TryGetCount(type, enumerable);
            List<string> parts = new List<string>(MaxCollectionItems + 1);
            int index = 0;
            bool hasMore = false;

            foreach (object item in enumerable)
            {
                if (index >= MaxCollectionItems)
                {
                    hasMore = true;
                    break;
                }

                parts.Add(FormatValue(item, depth + 1, true));
                index++;
            }

            if (totalCount >= 0)
                AddTruncationSuffix(parts, totalCount, index);
            else if (hasMore)
                parts.Add("...");

            return "[" + string.Join(", ", parts.ToArray()) + "]";
        }

        private static void AddTruncationSuffix(List<string> parts, int totalCount, int shownCount)
        {
            if (totalCount <= MaxCollectionItems || totalCount <= shownCount)
                return;

            parts.Add("... +" + (totalCount - shownCount).ToString(CultureInfo.InvariantCulture) + " more");
        }

        private static int TryGetCount(Type type, object value)
        {
            if (value == null)
                return -1;

            ICollection collection = value as ICollection;
            if (collection != null)
                return collection.Count;

            PropertyInfo countProperty = type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            if (countProperty != null && countProperty.PropertyType == typeof(int) && countProperty.GetIndexParameters().Length == 0)
            {
                try
                {
                    return (int)countProperty.GetValue(value, null);
                }
                catch
                {
                    return -1;
                }
            }

            return -1;
        }

        private static bool TryReadKeyValue(object entry, out object key, out object value)
        {
            key = null;
            value = null;

            if (entry == null)
                return false;

            Type entryType = entry.GetType();
            PropertyInfo keyProperty = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo valueProperty = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);

            if (keyProperty == null || valueProperty == null)
                return false;

            try
            {
                key = keyProperty.GetValue(entry, null);
                value = valueProperty.GetValue(entry, null);
                return true;
            }
            catch
            {
                key = null;
                value = null;
                return false;
            }
        }

        private static bool IsGenericDictionaryLike(Type type)
        {
            if (type == null)
                return false;

            if (IsGenericDictionaryInterface(type))
                return true;

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (IsGenericDictionaryInterface(interfaces[i]))
                    return true;
            }

            return false;
        }

        private static bool IsGenericDictionaryInterface(Type type)
        {
            if (type == null || !type.IsGenericType)
                return false;

            Type definition = type.GetGenericTypeDefinition();
            return definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>);
        }

        private static bool IsPrimitiveLike(Type type)
        {
            return type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            if (type == null)
                return new FieldInfo[0];

            List<FieldInfo> fields = new List<FieldInfo>();
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] allFields = type.GetFields(Flags);

            for (int i = 0; i < allFields.Length; i++)
            {
                FieldInfo field = allFields[i];
                if (field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                    continue;

                bool isUnitySerializable = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
                if (!isUnitySerializable)
                    continue;

                fields.Add(field);
            }

            return fields.ToArray();
        }

        private static string QuoteString(string value)
        {
            if (value == null)
                return "\"\"";

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string GetCallerName(string callerFilePath)
        {
            if (string.IsNullOrEmpty(callerFilePath))
                return "Unknown";

            if (CallerNameCache.TryGetValue(callerFilePath, out string cachedName))
                return cachedName;

            string name = ExtractFileNameWithoutExtension(callerFilePath);
            CallerNameCache[callerFilePath] = name;
            return name;
        }

        private static string ExtractFileNameWithoutExtension(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            int backslashIndex = path.LastIndexOf('\\');
            int startIndex = slashIndex > backslashIndex ? slashIndex : backslashIndex;
            startIndex += 1;

            int dotIndex = path.LastIndexOf('.');

            if (dotIndex <= startIndex)
                dotIndex = path.Length;

            int length = dotIndex - startIndex;

            if (length <= 0)
                return "Unknown";

            return path.Substring(startIndex, length);
        }

#if UNITY_EDITOR
        private static string Colorize(string prefix, NeoDebugType type)
        {
            Color color;
            switch (type)
            {
                case NeoDebugType.Warning:
                    color = NeoDebugSettings.WarningPrefixColor;
                    break;
                case NeoDebugType.Error:
                    color = NeoDebugSettings.ErrorPrefixColor;
                    break;
                default:
                    color = NeoDebugSettings.LogPrefixColor;
                    break;
            }

            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + prefix + "</color>";
        }
#endif
    }
}
