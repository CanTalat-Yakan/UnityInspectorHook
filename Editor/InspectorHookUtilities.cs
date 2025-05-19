#if UNITY_EDITOR
using System;
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

        private static void Iterate(SerializedProperty property, Action<SerializedProperty> onProcessProperty)
        {
            var current = property.Copy();
            var parentDepth = current.depth;

            do
            {
                onProcessProperty(current.Copy());

                bool isExpanded = current.isExpanded;
                bool hasChildren = current.hasVisibleChildren;

                if (isExpanded && hasChildren)
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

        public static bool TryGetAttributes<T>(SerializedProperty property, out T[] attributes) where T : class
        {
            var field = GetSerializedFieldInfo(property);
            attributes = field?.GetCustomAttributes(typeof(T), true).Cast<T>().ToArray() ?? default;
            return attributes.Length > 0;
        }

        public static bool TryGetAttribute<T>(SerializedProperty property, out T attribute) where T : class
        {
            attribute = null;
            var field = GetSerializedFieldInfo(property);
            return (attribute = field?.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) != null;
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