#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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
    public static class InspectorHook
    {
        private static List<HookEntry> s_onInitialization= new();
        private static List<HookPropertyEntry> s_onProcessProperty = new();
        private static List<HookMethodEntry> s_onProcessMethod = new();
        private static List<HookEntry> s_onPostProcess = new();

        private static HashSet<string> s_handledProperties = new();
        private static HashSet<MethodInfo> s_handledMethods = new();

        public static MonoBehaviour Target { get; private set; }
        public static SerializedObject SerializedObject { get; private set; }

        /// <summary>
        /// Marks the specified property as handled by adding its path to the internal collection of handled properties.
        /// </summary>
        /// <param name="propertyPath">The path of the property to mark as handled. This value cannot be <see langword="null"/> or empty.</param>
        public static void MarkPropertyAsHandled(string propertyPath) =>
            s_handledProperties.Add(propertyPath);

        public static void MarkPropertyAsHandled(MethodInfo method) =>
            s_handledMethods.Add(method);

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

        /// <summary>
        /// Determines whether the specified property path is handled.
        /// </summary>
        /// <param name="propertyPath">The path of the property to check. This must be a non-null, non-empty string.</param>
        /// <returns><see langword="true"/> if the specified property path is handled; otherwise, <see langword="false"/>.</returns>
        public static bool IsPropertyHandled(string propertyPath) =>
            s_handledProperties.Contains(propertyPath);

        public static bool IsMethodHandled(MethodInfo method) =>
            s_handledMethods.Contains(method);

        /// <summary>
        /// Resets the collection of handled properties to its initial state.
        /// </summary>
        /// <remarks>This method clears all entries from the internal collection of handled properties. It
        /// is typically used to reset the state of the system when reinitialization is required.</remarks>
        public static void ResetHandledProperties() =>
            s_handledProperties.Clear();

        /// <summary>
        /// Adds an initialization hook to be executed with a specified priority.
        /// </summary>
        /// <param name="hook">The action to be executed during initialization. Cannot be <see langword="null"/>.</param>
        /// <param name="priority">The priority of the hook. Hooks with higher priority values are executed earlier. The default is 0.</param>
        public static void AddInitialization(Action hook, int priority = 0)
        {
            s_onInitialization.Add(new() { Hook = hook, Priority = priority });
            s_onInitialization.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        /// <summary>
        /// Adds a process property hook with the specified priority to be executed during property processing.
        /// </summary>
        /// <remarks>Hooks are stored and executed in descending order of priority. Adding a hook with the
        /// same priority as an existing one does not overwrite the existing hook; both will be executed in the order
        /// they were added.</remarks>
        /// <param name="hook">The action to be invoked for processing a serialized property. This parameter cannot be <see
        /// langword="null"/>.</param>
        /// <param name="priority">The priority of the hook. Hooks with higher priority values are executed before those with lower priority
        /// values. The default value is 0.</param>
        public static void AddProcessProperty(Action<SerializedProperty> hook, int priority = 0)
        {
            s_onProcessProperty.Add(new() { Hook = hook, Priority = priority });
            s_onProcessProperty.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        /// <summary>
        /// Registers a method hook to be invoked during the processing of methods, with an optional priority.
        /// </summary>
        /// <remarks>Hooks are stored and executed in descending order of priority. This allows
        /// higher-priority hooks to take precedence.</remarks>
        /// <param name="hook">The action to execute, which receives a <see cref="MethodInfo"/> representing the method being processed.</param>
        /// <param name="priority">The priority of the hook. Hooks with higher priority values are executed before those with lower values. 
        /// The default priority is 0.</param>
        public static void AddProcessMethod(Action<MethodInfo> hook, int priority = 0)
        {
            s_onProcessMethod.Add(new() { Hook = hook, Priority = priority });
            s_onProcessMethod.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        /// <summary>
        /// Adds a post-processing action to be executed, with an optional priority.
        /// </summary>
        /// <remarks>Post-processing actions are executed in descending order of priority. If multiple
        /// actions have the same priority,  their execution order is undefined relative to each other.</remarks>
        /// <param name="hook">The action to be executed during post-processing. This parameter cannot be <see langword="null"/>.</param>
        /// <param name="priority">The priority of the action. Actions with higher priority values are executed before those with lower values.
        /// The default priority is 0.</param>
        public static void AddPostProcess(Action hook, int priority = 0)
        {
            s_onPostProcess.Add(new() { Hook = hook, Priority = priority });
            s_onPostProcess.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Initializes the editor by setting up the serialized object and target, and invoking all registered
        /// initialization hooks.
        /// </summary>
        /// <remarks>This method assigns the provided editor's serialized object and target to static
        /// fields,  and then iterates through all registered initialization hooks, invoking each one. Ensure that the
        /// editor instance is valid and properly configured before calling this method.</remarks>
        /// <param name="editor">The editor instance to initialize. Must not be null.</param>
        public static void InvokeInitialization(Editor editor)
        {
            SerializedObject = editor.serializedObject;
            Target = editor.target as MonoBehaviour;

            foreach (var entry in s_onInitialization)
                entry.Hook();
        }

        /// <summary>
        /// Invokes all registered hooks to process the specified serialized property.
        /// </summary>
        /// <remarks>This method iterates through all registered hooks and invokes them with the provided
        /// serialized property. Hooks are expected to perform custom processing or modifications on the
        /// property.</remarks>
        /// <param name="serializedProperty">The serialized property to be processed by the registered hooks. Cannot be null.</param>
        public static void InvokeProcessProperties(SerializedProperty serializedProperty)
        {
            foreach (var entry in s_onProcessProperty)
                entry.Hook(serializedProperty);
        }

        /// <summary>
        /// Invokes the specified method on all registered process hooks.
        /// </summary>
        /// <remarks>This method iterates through all registered hooks and invokes the provided method on
        /// each of them. Ensure that <paramref name="methodInfo"/> represents a valid method to avoid runtime
        /// errors.</remarks>
        /// <param name="methodInfo">The method information to be invoked on each process hook. Cannot be <see langword="null"/>.</param>
        public static void InvokeProcessMethod(MethodInfo methodInfo)
        {
            foreach (var entry in s_onProcessMethod)
                entry.Hook(methodInfo);
        }
        
        /// <summary>
        /// Invokes all registered post-processing hooks.
        /// </summary>
        /// <remarks>This method iterates through the collection of registered post-processing entries and
        /// invokes their associated hooks. It is typically used to execute any additional processing steps that have
        /// been registered in advance.</remarks>
        public static void InvokePostProcess()
        {
            foreach (var entry in s_onPostProcess)
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
        public static void GetAllMethods(out List<MethodInfo> methodInfos)
        {
            methodInfos = new();
            InspectorHookUtilities.IterateMethods(methodInfos.Add);
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