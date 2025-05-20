#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public class InspectorHookUtilities : MonoBehaviour
    {
        public static void IterateMethods(Action<MethodInfo> onProcessMethod)
        {
            var methods = InspectorHook.Target.GetType().GetMethods();
            foreach (var method in methods)
                onProcessMethod(method);
        }

        public static void IterateProperties(Action<SerializedProperty> onProcessProperty)
        {
            var iterator = InspectorHook.SerializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script field
            if (iterator.NextVisible(true))
                Iterate(iterator, onProcessProperty);
        }

        public static void Iterate(SerializedProperty property, Action<SerializedProperty> onProcessProperty)
        {
            var current = property.Copy();
            var parentDepth = current.depth;

            do
            {
                onProcessProperty(current.Copy());

                bool isExpanded = current.isExpanded;
                bool hasChildren = current.hasVisibleChildren;
                bool isArray = current.isArray;
                Type fieldType = GetSerializedFieldInfo(current)?.FieldType;
                bool isCollection = IsCollection(fieldType);
                bool isDictionary = IsDictionary(fieldType);

                if (isExpanded && hasChildren && !isArray && !isCollection && !isDictionary)
                {
                    var child = current.Copy();
                    if (child.NextVisible(true) && child.depth > parentDepth)
                    {
                        Iterate(child, onProcessProperty);
                        current = child;
                        continue;
                    }
                }
            }
            while (current.NextVisible(false) && current.depth >= parentDepth);
        }

        public static bool IsCollection(Type fieldType)
        {
            if (fieldType == null)
                return false;

            return fieldType.IsArray || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>));
        }

        public static bool IsDictionary(Type fieldType)
        {
            if (fieldType == null)
                return false;

            return typeof(IDictionary).IsAssignableFrom(fieldType) ||
                                fieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        public static bool IsArrayElement(SerializedProperty property)
        {
            string path = property.propertyPath;

            int dotIndex = path.IndexOf('.');
            if (dotIndex == -1)
                return false;

            string propertyName = path.Substring(0, dotIndex);
            return property.serializedObject.FindProperty(propertyName).isArray;
        }

        public static bool IsGenericWithChildren(SerializedProperty property)
        {
            if (property == null)
                return false;

            return property.propertyType == SerializedPropertyType.Generic
                && property.hasVisibleChildren
                && !property.type.StartsWith("PPtr<"); // Avoid UnityEngine.Object refs
        }

        public static string GetToolTip(SerializedProperty property)
        {
            TryGetAttribute<TooltipAttribute>(property, out var tooltip);
            return tooltip?.tooltip ?? ObjectNames.NicifyVariableName(property.name);
        }

        public static bool TryGetAttributes<T>(SerializedProperty property, out T[] attributes) where T : class
        {
            var field = GetSerializedFieldInfo(property);
            attributes = field?.GetCustomAttributes(typeof(T), true).Cast<T>().ToArray() ?? default;
            return attributes?.Length > 0;
        }

        public static bool TryGetAttribute<T>(SerializedProperty property, out T attribute) where T : class
        {
            attribute = null;
            var field = GetSerializedFieldInfo(property);
            return (attribute = field?.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) != null;
        }

        public static bool TryGetAttributes<T>(MethodInfo method, out T[] attributes) where T : class
        {
            attributes = method?.GetCustomAttributes(typeof(T), true).Cast<T>().ToArray() ?? default;
            return attributes?.Length > 0;
        }

        public static bool TryGetAttribute<T>(MethodInfo method, out T attribute) where T : class
        {
            attribute = null;
            return (attribute = method?.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) != null;
        }

        public static FieldInfo GetSerializedFieldInfo(SerializedProperty property)
        {
            var targetObject = property.serializedObject.targetObject;
            var pathSegments = property.propertyPath.Split('.');
            FieldInfo fieldInfo = null;
            Type currentType = targetObject.GetType();

            foreach (var segment in pathSegments)
            {
                if (segment.StartsWith("Array.data["))
                    continue;

                // 1. Search through all base types
                Type typeToCheck = currentType;
                while (typeToCheck != null && typeToCheck != typeof(object))
                {
                    fieldInfo = typeToCheck.GetField(segment,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (fieldInfo != null)
                        break;

                    typeToCheck = typeToCheck.BaseType;
                }

                if (fieldInfo == null)
                    return null;

                // 2. Handle Unity serialization special cases
                currentType = fieldInfo.FieldType;

                // Handle List/Array element types
                if (currentType.IsArray)
                    currentType = currentType.GetElementType();
                else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(List<>))
                    currentType = currentType.GetGenericArguments()[0];
            }

            return fieldInfo;
        }
    }
}
#endif