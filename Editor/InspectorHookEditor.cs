#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class InspectorHookEditor : Editor
    {
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

        private void ProcessProperty(SerializedProperty property)
        {
            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            InspectorHook.InvokeProcessProperties(property);

            if (InspectorHook.IsPropertyHandled(property.propertyPath))
                return;

            EditorGUI.indentLevel = property.depth;
            bool isExpanded = EditorGUILayout.PropertyField(property, false);
            InspectorHook.MarkPropertyAsHandled(property.propertyPath);
        }
    }
}
#endif