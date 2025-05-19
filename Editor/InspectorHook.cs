using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public struct HookInitializeEntry
    {
        public Action Hook;
        public int Priority;
    }

    public struct HookProcessPropertyEntry
    {
        public Action<SerializedProperty> Hook;
        public int Priority;
    }
    
    public struct HookProcessMethodEntry
    {
        public Action<MethodInfo> Hook;
        public int Priority;
    }

    public static class InspectorHook
    {
        private static List<HookInitializeEntry> s_onInitialization= new();
        private static List<HookProcessPropertyEntry> s_onProcessProperty = new();
        private static List<HookProcessMethodEntry> s_onProcessMethod = new();
        private static HashSet<string> s_handledProperties = new();

        public static MonoBehaviour Target { get; private set; }
        public static SerializedObject SerializedObject { get; private set; }

        public static void MarkPropertyAsHandled(string propertyPath) =>
            s_handledProperties.Add(propertyPath);

        public static bool IsPropertyHandled(string propertyPath) =>
            s_handledProperties.Contains(propertyPath);

        public static void ResetHandledProperties() =>
            s_handledProperties.Clear();

        public static void AddInitialization(Action hook, int priority = 0)
        {
            s_onInitialization.Add(new() { Hook = hook, Priority = priority });
            s_onInitialization.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        public static void AddProcessProperty(Action<SerializedProperty> hook, int priority = 0)
        {
            s_onProcessProperty.Add(new() { Hook = hook, Priority = priority });
            s_onProcessProperty.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        public static void AddProcessMethod(Action<MethodInfo> hook, int priority = 0)
        {
            s_onProcessMethod.Add(new() { Hook = hook, Priority = priority });
            s_onProcessMethod.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static void InvokeInitialization(Editor editor)
        {
            SerializedObject = editor.serializedObject;
            Target = editor.target as MonoBehaviour;

            foreach (var entry in s_onInitialization)
                entry.Hook();
        }

        public static void InvokeProcessProperties(SerializedProperty serializedProperty)
        {
            foreach (var entry in s_onProcessProperty)
                entry.Hook(serializedProperty);
        }

        public static void InvokeProcessMethod(MethodInfo methodInfo)
        {
            foreach (var entry in s_onProcessMethod)
                entry.Hook(methodInfo);
        }

        public static void DrawProperty(SerializedProperty property, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(property, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
        }
    }

    [CustomEditor(typeof(MonoBehaviour), true)]
    public class InspectorHookEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            InspectorHook.ResetHandledProperties();
            InspectorHook.InvokeInitialization(this);

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script field

            while (iterator.NextVisible(false))
            {
                InspectorHook.InvokeProcessProperties(iterator.Copy());

                if (!InspectorHook.IsPropertyHandled(iterator.propertyPath))
                {
                    EditorGUILayout.PropertyField(iterator, false);
                    InspectorHook.MarkPropertyAsHandled(iterator.propertyPath);
                }
            }

            var methods = target.GetType().GetMethods();
            foreach (var method in methods)
                InspectorHook.InvokeProcessMethod(method);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
