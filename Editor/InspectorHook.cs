#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEssentials
{
    /// <summary>
    /// Represents an entry in a hook system, containing a callback action and its associated priority.
    /// </summary>
    /// <remarks>The <see cref="HookEntry"/> struct is typically used to define a hook with a specific
    /// priority level. Hooks with higher priority values are generally executed before those with lower priority
    /// values.</remarks>
    public struct HookEntry
    {
        public Action Hook;
        public int Priority;
    }

    /// <summary>
    /// Represents an entry that associates a hook action with a priority for processing serialized properties.
    /// </summary>
    /// <remarks>This structure is typically used to define a hook action that operates on a <see
    /// cref="SerializedProperty"/>  and assigns it a priority to determine the order of execution when multiple hooks
    /// are applied.</remarks>
    public struct HookPropertyEntry
    {
        public Action<SerializedProperty> Hook;
        public int Priority;
    }

    /// <summary>
    /// Represents an entry for a method hook, including the hook action and its execution priority.
    /// </summary>
    /// <remarks>This structure is typically used to define a method hook, where the <see cref="Hook"/> action
    /// is invoked with the target method's metadata, and the <see cref="Priority"/> determines the order in which hooks
    /// are executed relative to others.</remarks>
    public struct HookMethodEntry
    {
        public Action<MethodInfo> Hook;
        public int Priority;
    }

    /// <summary>
    /// Provides a centralized mechanism for managing hooks and custom processing logic  during the Unity Inspector's
    /// lifecycle, including initialization, property processing,  method processing, and post-processing.
    /// </summary>
    /// <remarks>The <see cref="InspectorHook"/> class allows developers to register and invoke custom hooks 
    /// for various stages of the Unity Inspector's workflow. It supports prioritized execution of hooks  and tracks
    /// handled properties to prevent duplicate processing. This class is particularly useful  for extending the Unity
    /// Editor with custom behaviors and workflows.  Key features include: - Registering hooks for initialization,
    /// property processing, method processing, and post-processing. - Tracking and marking properties as handled to
    /// avoid redundant operations. - Utilities for retrieving all serialized properties and methods of the target
    /// object. - Drawing properties in the Inspector while marking them as handled.  This class is designed for use in
    /// Unity Editor extensions and assumes familiarity with Unity's  SerializedObject, SerializedProperty, and Editor
    /// APIs.</remarks>
    [DefaultExecutionOrder(-2000)]
    public static class InspectorHook
    {
        private static List<HookEntry> s_onInitialization = new();
        private static List<HookPropertyEntry> s_onProcessProperty = new();
        private static List<HookMethodEntry> s_onProcessMethod = new();
        private static List<HookEntry> s_onPreProcess = new();
        private static List<HookEntry> s_onPostProcess = new();

        private static HashSet<string> s_disabledProperties = new();
        private static HashSet<string> s_handledProperties = new();
        private static HashSet<MethodInfo> s_disabledMethods = new();
        private static HashSet<MethodInfo> s_handledMethods = new();

        public static Object Target { get; private set; }
        public static Object[] Targets { get; private set; }
        public static SerializedObject SerializedObject { get; private set; }
        public static bool Initialized { get; private set; } = false;

        [InitializeOnLoadMethod]
        public static void RegisterInspectorHookStateReset() =>
            Selection.selectionChanged += () => ResetState();

        public static void ResetState()
        {
            Target = null;
            Targets = null;
            SerializedObject = null;
            Initialized = false;

            ResetHandledDisabledProperties();
            ResetHandledDisabledMethods();
        }

        public static void ResetHandledDisabledProperties()
        {
            s_handledProperties.Clear();
            s_disabledProperties.Clear();
        }

        public static void ResetHandledDisabledMethods()
        {
            s_handledMethods.Clear();
            s_disabledMethods.Clear();
        }

        public static void MarkPropertyAsHandled(string propertyPath) =>
            s_handledProperties.Add(propertyPath);

        public static void MarkPropertyDisabled(string propertyPath) =>
            s_disabledProperties.Add(propertyPath);

        public static void MarkMethodAsHandled(MethodInfo method) =>
            s_handledMethods.Add(method);

        public static void MarkMethodDisabled(MethodInfo method) =>
            s_disabledMethods.Add(method);

        public static void MarkPropertyAndChildrenAsHandled(SerializedProperty property)
        {
            MarkPropertyAsHandled(property.propertyPath);

            if (property.hasVisibleChildren)
            {
                var iterator = property.Copy();
                var endProperty = iterator.GetEndProperty();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
                {
                    MarkPropertyAsHandled(iterator.propertyPath);
                    enterChildren = false;
                }
            }
        }

        public static void MarkPropertyAndChildrenDisabled(SerializedProperty property)
        {
            MarkPropertyDisabled(property.propertyPath);

            if (property.hasVisibleChildren)
            {
                var iterator = property.Copy();
                var endProperty = iterator.GetEndProperty();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
                {
                    MarkPropertyDisabled(iterator.propertyPath);
                    enterChildren = false;
                }
            }
        }

        public static bool IsPropertyHandled(string propertyPath) =>
            s_handledProperties.Contains(propertyPath);

        public static bool IsPropertyDisabled(string propertyPath) =>
            s_disabledProperties.Contains(propertyPath);

        public static bool IsMethodHandled(MethodInfo method) =>
            s_handledMethods.Contains(method);

        public static bool IsMethodDisabled(MethodInfo method) =>
            s_disabledMethods.Contains(method);

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

        public static void AddPreProcess(Action hook, int priority = 0)
        {
            s_onPreProcess.Add(new() { Hook = hook, Priority = priority });
            s_onPreProcess.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static void InvokeInitialization(Editor editor)
        {
            Initialized = true;
            SerializedObject = editor.serializedObject;
            Targets = editor.targets;
            Target = editor.target;

            foreach (var entry in s_onInitialization)
                entry.Hook();
        }

        public static void InvokeProcessProperties(SerializedProperty serializedProperty)
        {
            Target = InspectorHookUtilities.GetTargetObjectOfProperty(serializedProperty) as Object;

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

        public static void InvokePreProcess()
        {
            foreach (var entry in s_onPreProcess)
                entry.Hook();
        }

        /// <summary>
        /// Retrieves all serialized properties and outputs them as a list.
        /// </summary>
        /// <remarks>This method initializes the <paramref name="serializedProperties"/> parameter and
        /// populates it with all serialized properties by iterating through them. The caller is responsible for
        /// handling the returned list.</remarks>
        /// <param name="serializedProperties">When this method returns, contains a list of all serialized properties. This parameter is passed
        /// uninitialized.</param>
        public static void GetAllProperties(out List<SerializedProperty> serializedProperties)
        {
            serializedProperties = new();
            InspectorHookUtilities.IterateProperties(serializedProperties.Add);
        }

        /// <summary>
        /// Retrieves all methods and stores them in the provided list.
        /// </summary>
        /// <param name="methodInfos">When this method returns, contains a list of <see cref="MethodInfo"/> objects representing the retrieved
        /// methods. The list is initialized within the method and populated with the results.</param>
        public static void GetAllMethods(out List<MethodInfo> methodInfos) =>
            GetAllMethods(true, out methodInfos);

        public static void GetAllMethods(bool recursively, out List<MethodInfo> methodInfos)
        {
            methodInfos = new();

            if (Target != null)
                InspectorHookUtilities.IterateMethods(Target.GetType(), methodInfos.Add);

            if (!recursively)
                return;

            GetAllProperties(out var properties);
            foreach (var property in properties)
            {
                var fieldInfo = InspectorHookUtilities.GetSerializedFieldInfo(property);
                InspectorHookUtilities.IterateMethods(fieldInfo?.FieldType, methodInfos.Add);
            }
        }

        /// <summary>
        /// Renders the specified serialized property in the Unity Editor and optionally includes its child properties.
        /// </summary>
        /// <remarks>This method uses Unity's <see cref="EditorGUILayout.PropertyField"/> to render the
        /// property in the editor. It also marks the property as handled internally to prevent duplicate
        /// processing.</remarks>
        /// <param name="property">The <see cref="SerializedProperty"/> to render. Cannot be <see langword="null"/>.</param>
        /// <param name="includeChildren">A value indicating whether to include child properties in the rendering.  <see langword="true"/> to include
        /// child properties; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the property is expanded in the editor; otherwise, <see langword="false"/>.</returns>
        public static bool DrawProperty(SerializedProperty property, bool includeChildren = false)
        {
            var isExpanded = EditorGUILayout.PropertyField(property, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
            return isExpanded;
        }

        /// <summary>
        /// Renders a property field in the Unity Editor and optionally includes its child properties.
        /// </summary>
        /// <remarks>This method is typically used in custom editor scripts to draw serialized properties
        /// in the Unity Inspector. It also marks the property as handled internally to prevent duplicate
        /// processing.</remarks>
        /// <param name="position">The screen rectangle that specifies the position and size of the property field.</param>
        /// <param name="property">The <see cref="SerializedProperty"/> to be drawn. Cannot be <see langword="null"/>.</param>
        /// <param name="includeChildren"><see langword="true"/> to include child properties in the rendering; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the property is expanded to show its child properties; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool DrawProperty(Rect position, SerializedProperty property, bool includeChildren = false)
        {
            var isExpanded = EditorGUI.PropertyField(position, property, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
            return isExpanded;
        }

        /// <summary>
        /// Renders a property field in the Unity Editor and optionally includes its child properties.
        /// </summary>
        /// <remarks>This method is typically used in custom editor scripts to render and manage
        /// serialized properties in the Unity Inspector. It also marks the property as handled internally to prevent
        /// duplicate processing.</remarks>
        /// <param name="position">The position and size of the property field, specified as a <see cref="Rect"/>.</param>
        /// <param name="property">The <see cref="SerializedProperty"/> to be drawn. This cannot be <see langword="null"/>.</param>
        /// <param name="label">The label to display alongside the property field. If <see langword="null"/>, the property's default label
        /// is used.</param>
        /// <param name="includeChildren"><see langword="true"/> to include child properties in the field; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the property is expanded to show its child properties; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool DrawProperty(Rect position, SerializedProperty property, GUIContent label, bool includeChildren = false)
        {
            var isExpanded = EditorGUI.PropertyField(position, property, label, includeChildren);
            MarkPropertyAsHandled(property.propertyPath);
            return isExpanded;
        }
    }
}
#endif