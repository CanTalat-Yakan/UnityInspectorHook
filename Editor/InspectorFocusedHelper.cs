#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public static class InspectorFocusedHelper
    {
        public static bool ProcessKeyboardClick(Rect position, bool drawOutline = true)
        {
            var controlId = GetControlId(position);
            var isControlFocused = GUIUtility.keyboardControl == controlId;

            if (isControlFocused && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Event.current.Use();
                return true;
            }

            if (isControlFocused && drawOutline)
                GUI.DrawTexture(position, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0.25f, 0.5f, 1f, 1f), 1, 1);

            return false;
        }

        public static int GetControlId(Rect position) =>
            GUIUtility.GetControlID(FocusType.Keyboard, position);
    }
}
#endif