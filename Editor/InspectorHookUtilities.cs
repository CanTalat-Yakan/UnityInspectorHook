#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEssentials
{
    /// <summary>
    /// Provides utility methods for working with Unity's <see cref="SerializedProperty"/> and reflection-based
    /// operations, such as iterating over methods, properties, and attributes.
    /// </summary>
    /// <remarks>This class is designed to assist with common tasks in Unity's editor scripting, such as
    /// inspecting serialized objects, retrieving metadata, and processing collections of properties or methods. It
    /// includes methods for iterating over serialized properties, determining property types, and accessing custom
    /// attributes.</remarks>
    public class InspectorHookUtilities : MonoBehaviour
    {
        /// <summary>
        /// Iterates through all methods of the target object and invokes the specified action for each method.
        /// </summary>
        /// <remarks>This method retrieves all methods of the target object using reflection and applies
        /// the provided action to each method. The target object is determined by the
        /// <c>InspectorHook.Target</c>.</remarks>
        /// <param name="onProcessMethod">An action to perform on each <see cref="MethodInfo"/> object representing a method of the target object.
        /// This parameter cannot be <see langword="null"/>.</param>
        public static void IterateMethods(Action<MethodInfo> onProcessMethod)
        {
            if (!InspectorHook.Initialized)
                return;

            var methods = InspectorHook.Target.GetType().GetMethods();
            foreach (var method in methods)
                onProcessMethod(method);
        }

        /// <summary>
        /// Iterates over the visible properties of a serialized object and applies the specified action to each
        /// property.
        /// </summary>
        /// <remarks>This method skips the script field and begins processing from the next visible
        /// property.  Ensure that <see cref="InspectorHook.SerializedObject"/> is properly initialized before calling
        /// this method.</remarks>
        /// <param name="onProcessProperty">An <see cref="Action{T}"/> delegate that is invoked for each visible <see cref="SerializedProperty"/>.  The
        /// delegate receives the current property as its parameter.</param>
        public static void IterateProperties(Action<SerializedProperty> onProcessProperty)
        {
            if (!InspectorHook.Initialized)
                return;


            var iterator = InspectorHook.SerializedObject?.GetIterator();

            if (!DrawScriptField(iterator))
                iterator.NextVisible(true); // Skip script field
            if (iterator.NextVisible(true))
                Iterate(iterator, onProcessProperty);
        }

        private static bool DrawScriptField(SerializedProperty iterator)
        {
            // Check if there is any property after m_Script
            var temp = iterator.Copy();
            int visibleCount = 0;
            while (temp.NextVisible(true))
                visibleCount++;

            return visibleCount == 1;

        }

        /// <summary>
        /// Iterates through the specified <see cref="SerializedProperty"/> and processes each visible property using
        /// the provided action.
        /// </summary>
        /// <remarks>This method traverses the hierarchy of the given <see cref="SerializedProperty"/>,
        /// including its children, and invokes the <paramref name="onProcessProperty"/> action for each visible
        /// property. Nested properties are recursively iterated if they are expanded and meet specific conditions
        /// (e.g., not arrays, collections, or dictionaries).</remarks>
        /// <param name="property">The root <see cref="SerializedProperty"/> to iterate over. Must not be <c>null</c>.</param>
        /// <param name="onProcessProperty">An action to execute for each visible property. The action receives a copy of the current property as its
        /// parameter. Must not be <c>null</c>.</param>
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

        /// <summary>
        /// Retrieves the <see cref="FieldInfo"/> for the field represented by the specified <see
        /// cref="SerializedProperty"/>.
        /// </summary>
        /// <remarks>This method traverses the property path of the <paramref name="property"/> to locate
        /// the corresponding field,  including handling nested fields, base class fields, and special cases for Unity's
        /// serialization system,  such as arrays and generic lists.</remarks>
        /// <param name="property">The <see cref="SerializedProperty"/> for which to retrieve the corresponding <see cref="FieldInfo"/>.</param>
        /// <returns>The <see cref="FieldInfo"/> of the field represented by the <paramref name="property"/>,  or <see
        /// langword="null"/> if the field cannot be resolved.</returns>
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

        /// <summary>
        /// Retrieves the value of a <see cref="SerializedProperty"/> based on its type.
        /// </summary>
        /// <remarks>The method uses the <see cref="SerializedProperty.propertyType"/> to determine the
        /// appropriate value to return.  For example, if the property type is <see
        /// cref="SerializedPropertyType.Integer"/>, the method returns the <see
        /// cref="SerializedProperty.intValue"/>.</remarks>
        /// <param name="property">The <see cref="SerializedProperty"/> whose value is to be retrieved. This parameter cannot be <see
        /// langword="null"/>.</param>
        /// <returns>The value of the <paramref name="property"/> as an <see cref="object"/>, corresponding to its type.  Returns
        /// <see langword="null"/> if the property type is unsupported.</returns>
        public static object GetPropertyValue(SerializedProperty property) =>
            property?.propertyType switch
            {
                SerializedPropertyType.Integer => property.intValue,
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Float => property.floatValue,
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Color => property.colorValue,
                SerializedPropertyType.ObjectReference => property.objectReferenceValue,
                SerializedPropertyType.LayerMask => property.intValue,
                SerializedPropertyType.Enum => GetEnumValue(property, property.enumValueIndex),
                SerializedPropertyType.Vector2 => property.vector2Value,
                SerializedPropertyType.Vector3 => property.vector3Value,
                SerializedPropertyType.Vector4 => property.vector4Value,
                SerializedPropertyType.Rect => property.rectValue,
                SerializedPropertyType.ArraySize => property.arraySize,
                SerializedPropertyType.Character => (char)property.intValue,
                SerializedPropertyType.AnimationCurve => property.animationCurveValue,
                SerializedPropertyType.Bounds => property.boundsValue,
                SerializedPropertyType.Quaternion => property.quaternionValue,
                SerializedPropertyType.ExposedReference => property.exposedReferenceValue,
                SerializedPropertyType.Vector2Int => property.vector2IntValue,
                SerializedPropertyType.Vector3Int => property.vector3IntValue,
                SerializedPropertyType.RectInt => property.rectIntValue,
                SerializedPropertyType.BoundsInt => property.boundsIntValue,
                SerializedPropertyType.ManagedReference => property.managedReferenceValue,
                _ => null
            };

        public static Type GetEnumType(FieldInfo fieldInfo)
        {
            var enumType = fieldInfo.FieldType;
            if (enumType.IsArray)
                enumType = enumType.GetElementType();
            else if (enumType.IsGenericType)
                enumType = enumType.GetGenericArguments()[0];
            return enumType;
        }

        public static Type GetEnumType(SerializedProperty property)
        {
            // Try to get the FieldInfo for the property
            var fieldInfo = GetSerializedFieldInfo(property);
            if (fieldInfo != null)
                return GetEnumType(fieldInfo);
            return null;
        }

        public static Enum GetEnumValue(SerializedProperty property, int enumIndex)
        {
            var enumType = GetEnumType(property);
            if (!enumType.IsEnum)
                return null;
            if (enumIndex < 0 || enumIndex >= Enum.GetNames(enumType).Length)
                return null;

            return (Enum)Enum.ToObject(enumType, enumIndex);
        }

        public static Enum GetCurrentValue(Type enumType, SerializedProperty property)
        {
            var enumName = property.enumNames[property.enumValueIndex];
            return (Enum)Enum.Parse(enumType, enumName);
        }

        public static Enum GetCurrentValue(SerializedProperty property)
        {
            var enumType = GetEnumType(property);
            var enumName = property.enumNames[property.enumValueIndex];
            return (Enum)Enum.Parse(enumType, enumName);
        }

        public static Enum GetCurrentValue(FieldInfo fieldInfo, SerializedProperty property)
        {
            var enumType = GetEnumType(fieldInfo);
            var enumName = property.enumNames[property.enumValueIndex];
            return (Enum)Enum.Parse(enumType, enumName);
        }

        public static void SetEnumValue(SerializedProperty property, Enum newValue)
        {
            string newName = newValue.ToString();
            int newIndex = Array.IndexOf(property.enumNames, newName);
            SetEnumValue(property, newIndex);
        }

        public static void SetEnumValue(SerializedProperty property, int newIndex)
        {
            if (property != null && property.enumValueIndex != newIndex)
            {
                property.enumValueIndex = newIndex;
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif