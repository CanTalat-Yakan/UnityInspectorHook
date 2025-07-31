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
    [DefaultExecutionOrder(-2010)]
    public class InspectorHookEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            InspectorHook.ResetHandledDisabledProperties();
            InspectorHook.ResetHandledDisabledMethods();

            InspectorHook.InvokeInitialization(this);

            InspectorHookUtilities.IterateProperties(ProcessProperty);
            InspectorHookUtilities.DrawStaticFields(target.GetType());
            InspectorHookUtilities.IterateMethods(InspectorHook.InvokeProcessMethod);

            if (EditorGUI.EndChangeCheck())
            {
                InspectorHook.InvokePreProcess();
                serializedObject.ApplyModifiedProperties();
                InspectorHook.InvokePostProcess();
            }
        }

        private void ProcessProperty(SerializedProperty property)
        {
            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            InspectorHook.InvokeProcessProperties(property);

            if (InspectorHook.IsPropertyDisabled(property.propertyPath))
                GUI.enabled = false;

            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            EditorGUI.indentLevel = property.depth;

            var enterChildren = property.isArray;
            InspectorHook.DrawProperty(property, enterChildren);

            GUI.enabled = true;
        }
    }
}
#endif