#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public struct HookEntry
    {
        public Action Hook;
        public int Priority;
    }

    public struct HookPropertyEntry
    {
        public Action<SerializedProperty> Hook;
        public int Priority;
    }
    
    public struct HookMethodEntry
    {
        public Action<MethodInfo> Hook;
        public int Priority;
    }

    public static class InspectorHook
    {
        private static List<HookEntry> s_onInitialization= new();
        private static List<HookPropertyEntry> s_onProcessProperty = new();
        private static List<HookMethodEntry> s_onProcessMethod = new();
        private static List<HookEntry> s_onPostProcess = new();

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
        
        public static void AddPostProcess(Action hook, int priority = 0)
        {
            s_onPostProcess.Add(new() { Hook = hook, Priority = priority });
            s_onPostProcess.Sort((a, b) => b.Priority.CompareTo(a.Priority));
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
        
        public static void InvokePostProcess()
        {
            foreach (var entry in s_onPostProcess)
                entry.Hook();
        }

        public static void GetAllProperties(out List<SerializedProperty> serializedProperties)
        {
            serializedProperties = new();
            InspectorHookUtilities.IterateProperties(serializedProperties.Add);
        }

        public static void GetAllMethods(out List<MethodInfo> methodInfos)
        {
            methodInfos = new();
            InspectorHookUtilities.IterateMethods(methodInfos.Add);
        }

        public static void DrawProperty(SerializedProperty property, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(property, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
        }

        public static void DrawProperty(Rect rect, SerializedProperty property, bool includeChildren = false)
        {
            EditorGUI.PropertyField(rect, property, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
        }

        public static void DrawProperty(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren = false)
        {
            EditorGUI.PropertyField(rect, property, label, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
        }
    }
}
#endif