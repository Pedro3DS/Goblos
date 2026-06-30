#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoUnityObjectUtility
    {
        private static MethodInfo findObjectsByTypeNoSortMethod;
        private static MethodInfo findObjectsByTypeSortModeMethod;
        private static MethodInfo findObjectsOfTypeMethod;
        private static Type findObjectsSortModeType;
        private static object findObjectsSortModeNone;
        private static MethodInfo getEntityIdMethod;
        private static MethodInfo getInstanceIdMethod;
        private static bool reflectionInitialized;

        public static UnityEngine.Object[] FindObjectsByType(Type targetType)
        {
            if (targetType == null || !typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                return new UnityEngine.Object[0];

            EnsureReflectionInitialized();

            UnityEngine.Object[] objects;
            if (TryInvokeFindObjects(findObjectsByTypeNoSortMethod, new object[] { targetType }, out objects))
                return objects;

            if (findObjectsByTypeSortModeMethod != null && findObjectsSortModeNone != null &&
                TryInvokeFindObjects(findObjectsByTypeSortModeMethod, new object[] { targetType, findObjectsSortModeNone }, out objects))
                return objects;

            if (TryInvokeFindObjects(findObjectsOfTypeMethod, new object[] { targetType }, out objects))
                return objects;

            return new UnityEngine.Object[0];
        }

        public static T[] FindObjectsByType<T>() where T : UnityEngine.Object
        {
            UnityEngine.Object[] objects = FindObjectsByType(typeof(T));
            List<T> typedObjects = new List<T>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                T typedObject = objects[i] as T;
                if (typedObject != null)
                    typedObjects.Add(typedObject);
            }

            return typedObjects.ToArray();
        }

        public static string GetObjectIdentifier(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return string.Empty;

            EnsureReflectionInitialized();

            object identifier = InvokeIdentifierMethod(getEntityIdMethod, unityObject);
            if (identifier != null)
                return FormatIdentifier(identifier);

            identifier = InvokeIdentifierMethod(getInstanceIdMethod, unityObject);
            return identifier != null ? FormatIdentifier(identifier) : string.Empty;
        }

        private static void EnsureReflectionInitialized()
        {
            if (reflectionInitialized)
                return;

            reflectionInitialized = true;

            Type objectType = typeof(UnityEngine.Object);
            findObjectsByTypeNoSortMethod = FindStaticMethod(objectType, "FindObjectsByType", typeof(Type));

            findObjectsSortModeType = Type.GetType("UnityEngine.FindObjectsSortMode, UnityEngine.CoreModule");
            if (findObjectsSortModeType != null)
            {
                findObjectsByTypeSortModeMethod = FindStaticMethod(objectType, "FindObjectsByType", typeof(Type), findObjectsSortModeType);
                try
                {
                    findObjectsSortModeNone = Enum.Parse(findObjectsSortModeType, "None");
                }
                catch
                {
                    findObjectsSortModeNone = null;
                }
            }

            findObjectsOfTypeMethod = FindStaticMethod(objectType, "FindObjectsOfType", typeof(Type));
            getEntityIdMethod = FindInstanceMethod(objectType, "GetEntityId");
            getInstanceIdMethod = FindInstanceMethod(objectType, "GetInstanceID");
        }

        private static MethodInfo FindStaticMethod(Type ownerType, string methodName, params Type[] parameterTypes)
        {
            return FindMethod(ownerType, methodName, BindingFlags.Public | BindingFlags.Static, parameterTypes);
        }

        private static MethodInfo FindInstanceMethod(Type ownerType, string methodName, params Type[] parameterTypes)
        {
            return FindMethod(ownerType, methodName, BindingFlags.Public | BindingFlags.Instance, parameterTypes);
        }

        private static MethodInfo FindMethod(Type ownerType, string methodName, BindingFlags bindingFlags, params Type[] parameterTypes)
        {
            if (ownerType == null || string.IsNullOrEmpty(methodName))
                return null;

            MethodInfo[] methods = ownerType.GetMethods(bindingFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.IsGenericMethodDefinition)
                    continue;

                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                    continue;

                bool matches = true;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != parameterTypes[p])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return method;
            }

            return null;
        }

        private static bool TryInvokeFindObjects(MethodInfo method, object[] arguments, out UnityEngine.Object[] objects)
        {
            objects = null;
            if (method == null)
                return false;

            try
            {
                object result = method.Invoke(null, arguments);
                objects = ConvertToUnityObjectArray(result);
                return objects != null;
            }
            catch
            {
                objects = null;
                return false;
            }
        }

        private static UnityEngine.Object[] ConvertToUnityObjectArray(object result)
        {
            UnityEngine.Object[] direct = result as UnityEngine.Object[];
            if (direct != null)
                return direct;

            Array array = result as Array;
            if (array == null)
                return null;

            List<UnityEngine.Object> objects = new List<UnityEngine.Object>(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                UnityEngine.Object unityObject = array.GetValue(i) as UnityEngine.Object;
                if (unityObject != null)
                    objects.Add(unityObject);
            }

            return objects.ToArray();
        }

        private static object InvokeIdentifierMethod(MethodInfo method, UnityEngine.Object unityObject)
        {
            if (method == null || unityObject == null)
                return null;

            try
            {
                return method.Invoke(unityObject, null);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatIdentifier(object identifier)
        {
            if (identifier == null)
                return string.Empty;

            MethodInfo getRawData = identifier.GetType().GetMethod("GetRawData", BindingFlags.Public | BindingFlags.Instance);
            if (getRawData != null && getRawData.GetParameters().Length == 0)
            {
                try
                {
                    object rawData = getRawData.Invoke(identifier, null);
                    if (rawData != null)
                        return Convert.ToString(rawData, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // Fall back to ToString below.
                }
            }

            return Convert.ToString(identifier, CultureInfo.InvariantCulture);
        }
    }
}
#endif
