#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoCommandTypeUtility
    {
        private const BindingFlags SerializableFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Dictionary<Type, FieldInfo[]> SerializableFieldsCache = new Dictionary<Type, FieldInfo[]>();

        public static FieldInfo[] GetSerializableFields(Type type)
        {
            if (type == null)
                return new FieldInfo[0];

            FieldInfo[] cached;
            if (SerializableFieldsCache.TryGetValue(type, out cached))
                return cached;

            FieldInfo[] allFields = type.GetFields(SerializableFieldFlags);
            List<FieldInfo> fields = new List<FieldInfo>(allFields.Length);

            for (int i = 0; i < allFields.Length; i++)
            {
                FieldInfo field = allFields[i];
                if (!IsSerializableField(field))
                    continue;

                fields.Add(field);
            }

            cached = fields.ToArray();
            SerializableFieldsCache[type] = cached;
            return cached;
        }

        public static FieldInfo FindSerializableField(FieldInfo[] fields, string name)
        {
            if (fields == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field != null && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                    return field;
            }

            return null;
        }

        public static bool IsDictionaryLike(Type type)
        {
            Type keyType;
            Type valueType;
            return TryGetDictionaryTypes(type, out keyType, out valueType);
        }

        public static bool IsCollectionLike(Type type)
        {
            if (type == null || type == typeof(string) || IsDictionaryLike(type))
                return false;

            Type elementType;
            return TryGetCollectionElementType(type, out elementType);
        }

        public static Type GetCollectionElementType(Type type)
        {
            Type elementType;
            return TryGetCollectionElementType(type, out elementType) ? elementType : null;
        }

        public static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            keyType = null;
            valueType = null;

            if (type == null)
                return false;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type[] arguments = type.GetGenericArguments();
                keyType = arguments[0];
                valueType = arguments[1];
                return true;
            }

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType != null && interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    Type[] arguments = interfaceType.GetGenericArguments();
                    keyType = arguments[0];
                    valueType = arguments[1];
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetCollectionElementType(Type type, out Type elementType)
        {
            elementType = null;

            if (type == null || type == typeof(string))
                return false;

            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>) ||
                    definition == typeof(HashSet<>) ||
                    definition == typeof(Queue<>) ||
                    definition == typeof(Stack<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
            }

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType == null || !interfaceType.IsGenericType)
                    continue;

                Type definition = interfaceType.GetGenericTypeDefinition();
                if (definition == typeof(IList<>) ||
                    definition == typeof(ICollection<>) ||
                    definition == typeof(IEnumerable<>) ||
                    definition == typeof(ISet<>))
                {
                    elementType = interfaceType.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }

        public static void ClearCache()
        {
            SerializableFieldsCache.Clear();
        }

        private static bool IsSerializableField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                return false;

            return field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
        }
    }
}
#endif
