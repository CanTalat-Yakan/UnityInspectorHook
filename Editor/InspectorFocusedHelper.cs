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
    public static class InspectorFocusedHelper
    {
        /// <summary>
        /// Processes a keyboard click event for a specified rectangular area.
        /// </summary>
        /// <remarks>This method checks if the specified rectangular area is focused and processes a
        /// keyboard  Enter key press event. If the area is focused and <paramref name="drawOutline"/> is  <see
        /// langword="true"/>, an outline is drawn around the area.</remarks>
        /// <param name="position">The rectangular area to monitor for keyboard input.</param>
        /// <param name="drawOutline">A value indicating whether to draw an outline around the rectangular area when it is focused. Defaults to
        /// <see langword="true"/>.</param>
        /// <returns><see langword="true"/> if the Enter key is pressed while the rectangular area is focused;  otherwise, <see
        /// langword="false"/>.</returns>
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

        /// <summary>
        /// Generates a unique control ID for a GUI element within the specified position.
        /// </summary>
        /// <remarks>The control ID is generated using the <see cref="GUIUtility.GetControlID(FocusType,
        /// Rect)"/> method with a focus type of <see cref="FocusType.Keyboard"/>. This ensures the control can receive
        /// keyboard focus.</remarks>
        /// <param name="position">The rectangular area on the screen that the control occupies.</param>
        /// <returns>A unique integer identifier for the control, which can be used to manage focus and interaction within the
        /// GUI system.</returns>
        public static int GetControlId(Rect position) =>
            GUIUtility.GetControlID(FocusType.Keyboard, position);
    }
}
#endif