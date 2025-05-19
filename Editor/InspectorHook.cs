using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public struct HookEntry
    {
        public Action Hook;
        public int Priority;
    }

    public static class InspectorHook
    {
        private static List<HookEntry> s_onInspectorGUICallbacks = new();

        public static MonoBehaviour Target { get; private set; }
        public static SerializedObject SerializedObject { get; private set; }

        public static void Add(Action hook, int priority = 0)
        {
            s_onInspectorGUICallbacks.Add(new HookEntry { Hook = hook, Priority = priority });
            s_onInspectorGUICallbacks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static void Remove(Action hook)
        {
            // Remove all entries with matching hook delegate
            for (int i = s_onInspectorGUICallbacks.Count - 1; i >= 0; i--)
                if (s_onInspectorGUICallbacks[i].Hook == hook)
                    s_onInspectorGUICallbacks.RemoveAt(i);
        }

        public static void InvokeHooks(Editor editor)
        {
            Debug.Log(s_onInspectorGUICallbacks.Count + " Count Hooks");

            SerializedObject = editor.serializedObject;
            Target = editor.target as MonoBehaviour;

            foreach (var entry in s_onInspectorGUICallbacks)
                entry.Hook();
        }
    }

    [CustomEditor(typeof(MonoBehaviour), true)]
    public class InspectorHookEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //DrawDefaultInspector();

            InspectorHook.InvokeHooks(this);
        }
    }
}
