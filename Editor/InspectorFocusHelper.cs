#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Provides helper methods for handling keyboard focus and input events in the Unity Inspector.
    /// </summary>
    /// <remarks>This class contains utility methods to assist with managing keyboard focus and processing
    /// input events for specific UI elements in the Unity Editor. It is particularly useful for custom editor scripts
    /// that require interaction with rectangular areas in the Inspector.</remarks>
    public static class InspectorFocusHelper
    {
        public static bool ProcessKeyboardClick(Rect position) =>
            ProcessKeyboardClick(position, GetControlID(position));

        public static bool ProcessKeyboardClick(Rect position, out int controlID)
        {
            controlID = GetControlID(position);
            return ProcessKeyboardClick(position, controlID);
        }

        public static bool ProcessKeyboardClick(Rect position, int controlID)
        {
            DrawFocusedOutline(controlID, position);
            return ProcessKeyboardInput(controlID);
        }

        public static bool ProcessKeyboardInput(int controlID)
        {
            var isControlFocused = IsControlFocused(controlID);
            if (!isControlFocused)
                return false;

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private static Color _outlineColor = new Color(0.25f, 0.5f, 1f, 1f);
        public static void DrawFocusedOutline(int controlID, Rect position)
        {
            if (IsControlFocused(controlID))
                GUI.DrawTexture(position, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0, _outlineColor, 1, 1);
        }

        public static bool IsControlFocused(int controlID) =>
            GUIUtility.keyboardControl == controlID;

        public static void SetControlFocused(int controlID) =>
            GUIUtility.keyboardControl = controlID;

        /// <summary>
        /// Generates a unique control ID for a GUI element within the specified position.
        /// </summary>
        /// <remarks>The control ID is generated using the <see cref="GUIUtility.GetControlID(FocusType,
        /// Rect)"/> method with a focus type of <see cref="FocusType.Keyboard"/>. This ensures the control can receive
        /// keyboard focus.</remarks>
        /// <param name="position">The rectangular area on the screen that the control occupies.</param>
        /// <returns>A unique integer identifier for the control, which can be used to manage focus and interaction within the
        /// GUI system.</returns>
        public static int GetControlID(Rect position) =>
            GUIUtility.GetControlID(FocusType.Keyboard, position);
    }
}
#endif