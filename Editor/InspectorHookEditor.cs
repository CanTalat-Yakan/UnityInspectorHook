#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Provides a custom editor for MonoBehaviour-derived objects, enabling enhanced inspector functionality.
    /// </summary>
    /// <remarks>This editor class extends Unity's default inspector behavior by integrating custom hooks for
    /// processing properties and methods. It allows developers to define custom logic for rendering and handling
    /// inspector elements, making it easier to create dynamic and interactive inspector interfaces. <para> The <see
    /// cref="OnInspectorGUI"/> method is overridden to update the serialized object, process properties and methods
    /// using hooks, and apply any changes made in the inspector. This enables seamless integration of custom property
    /// and method handling logic. </para> <para> To use this editor, annotate a MonoBehaviour-derived class with the
    /// <see cref="CustomEditorAttribute"/>  and specify <see cref="InspectorHookEditor"/> as the custom editor type.
    /// </para></remarks>
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class InspectorHookEditor : Editor
    {
        /// <summary>
        /// Draws and processes the custom inspector GUI for the associated object.
        /// </summary>
        /// <remarks>This method is called by the Unity Editor to render and handle the inspector
        /// interface for the object. It updates the serialized object, processes properties and methods using hooks,
        /// and applies any changes made in the inspector.</remarks>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            InspectorHook.ResetHandledProperties();

            InspectorHook.InvokeInitialization(this);

            InspectorHookUtilities.IterateProperties(ProcessProperty);
            InspectorHookUtilities.IterateMethods(InspectorHook.InvokeProcessMethod);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                InspectorHook.InvokePostProcess();
            }
        }

        /// <summary>
        /// Processes the specified serialized property, applying custom handling and rendering logic.
        /// </summary>
        /// <remarks>This method checks if the property has already been handled using the <see
        /// cref="InspectorHook"/>  and skips further processing if so. If not handled, it invokes custom property
        /// processing logic  and renders the property using the appropriate indentation level.</remarks>
        /// <param name="property">The serialized property to process. Cannot be null.</param>
        private void ProcessProperty(SerializedProperty property)
        {
            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            InspectorHook.InvokeProcessProperties(property);

            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            EditorGUI.indentLevel = property.depth;

            InspectorHook.DrawProperty(property, false);
        }
    }
}
#endif