# Unity Essentials

This module is part of the Unity Essentials ecosystem and follows the same lightweight, editor-first approach.
Unity Essentials is a lightweight, modular set of editor utilities and helpers that streamline Unity development. It focuses on clean, dependency-free tools that work well together.

All utilities are under the `UnityEssentials` namespace.

```csharp
using UnityEssentials;
```

## Installation

Install the Unity Essentials entry package via Unity's Package Manager, then install modules from the Tools menu.

- Add the entry package (via Git URL)
    - Window → Package Manager
    - "+" → "Add package from git URL…"
    - Paste: `https://github.com/CanTalat-Yakan/UnityEssentials.git`

- Install or update Unity Essentials packages
    - Tools → Install & Update UnityEssentials
    - Install all or select individual modules; run again anytime to update

---

# Inspector Hook

> Quick overview: Global, editor-only hook system to extend MonoBehaviour inspectors. Register prioritized hooks for initialization, per‑property and per‑method processing, plus pre/post apply. Includes utilities for iterating properties/methods, drawing, reflection helpers, and keyboard‑focus helpers.

A small, global inspector pipeline that lets you inject custom logic into every MonoBehaviour inspector without writing separate custom editors. Register handlers that run in a clear order: Initialization → Property Processing → Method Processing → Pre/Apply/Post. Mark properties or methods as handled/disabled, draw with convenience wrappers, and use helper utilities for reflection, attributes, and keyboard focus.

![screenshot](Documentation/Screenshot.png)

## Features
- Global custom editor for all `MonoBehaviour` types
  - Runs as a fallback custom editor; type‑specific CustomEditors still take precedence
- Prioritized hooks (highest priority runs first)
  - `AddInitialization(Action, int priority = 0)`
  - `AddProcessProperty(Action<SerializedProperty>, int priority = 0)`
  - `AddProcessMethod(Action<MethodInfo>, int priority = 0)`
  - `AddPreProcess(Action, int priority = 0)` and `AddPostProcess(Action, int priority = 0)`
- Current context access
  - `InspectorHook.Target`, `InspectorHook.Targets`, `InspectorHook.SerializedObject`
  - Auto‑reset on selection change
- Handled/Disabled tracking
  - Properties: `MarkPropertyAsHandled/Disabled`, queries `IsPropertyHandled/Disabled`
  - Methods: `MarkMethodAsHandled/Disabled`, queries `IsMethodHandled/Disabled`
  - Convenience: `MarkPropertyAndChildrenAsHandled/Disabled`
- Drawing helpers
  - `DrawProperty(...)` overloads mark properties as handled automatically
- Utilities
  - Iterate properties: `InspectorHookUtilities.IterateProperties` and recursive `Iterate`
  - Iterate methods: `InspectorHookUtilities.IterateMethods(type)` or current target
  - Reflection: `GetSerializedFieldInfo`, `GetPropertyValue`, enum helpers
  - Type checks: `IsCollection`, `IsDictionary`, `IsArrayElement`, `IsGenericWithChildren`
  - Attributes: `TryGetAttribute(s)` for properties and methods
- Focus helper for custom controls
  - `InspectorFocusHelper.ProcessKeyboardClick` draws an outline when focused and returns true on Enter key

## Requirements
- Unity Editor 6000.0+ (Editor‑only; no runtime code)

## Usage

Register hooks once on load and customize Inspector behavior across all MonoBehaviours.

```csharp
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEssentials;

public static class InspectorHookSetup
{
    [InitializeOnLoadMethod]
    private static void Setup()
    {
        // Runs before drawing; good place to cache state or compute context
        InspectorHook.AddInitialization(() =>
        {
            // Example: access context
            var target = InspectorHook.Target as MonoBehaviour;
            // Debug.Log($"Inspector init for: {target}");
        }, priority: 100);

        // Intercept and customize properties
        InspectorHook.AddProcessProperty(property =>
        {
            // Example 1: Hide a field by disabling the default draw
            if (property.name == "internalId")
            {
                InspectorHook.MarkPropertyDisabled(property.propertyPath);
                return;
            }

            // Example 2: Custom draw and mark handled
            if (property.name == "displayName")
            {
                EditorGUI.indentLevel = property.depth;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(property, new GUIContent("Title"), includeChildren: false);
                if (GUILayout.Button("Uppercase", GUILayout.Width(90)))
                {
                    property.stringValue = property.stringValue?.ToUpperInvariant();
                }
                EditorGUILayout.EndHorizontal();

                // Mark as handled so the default drawer won’t draw it again
                InspectorHook.MarkPropertyAsHandled(property.propertyPath);
            }
        });

        // Discover methods and add UI (e.g., buttons) based on attributes
        InspectorHook.AddProcessMethod(method =>
        {
            // Draw a button for methods that opt‑in via a custom attribute
            if (InspectorHookUtilities.TryGetAttribute<ButtonAttribute>(method, out var _))
            {
                var nice = ObjectNames.NicifyVariableName(method.Name);
                if (GUILayout.Button(nice))
                {
                    foreach (var t in InspectorHook.Targets)
                        method.Invoke(t, null);
                }
                InspectorHook.MarkMethodAsHandled(method);
            }
        });

        // Before apply → good for validation
        InspectorHook.AddPreProcess(() =>
        {
            // Example: validate or normalize data before ApplyModifiedProperties
        });

        // After apply → good for side‑effects
        InspectorHook.AddPostProcess(() =>
        {
            // Example: repaint scene view or ping assets
        });
    }
}

// Example opt‑in attribute for method buttons
[AttributeUsage(AttributeTargets.Method)]
public class ButtonAttribute : Attribute { }
```

### Drawing and focus helpers
Use the built‑in drawing helpers to both render and mark handled in one call.

```csharp
InspectorHook.AddProcessProperty(p =>
{
    if (p.name == "speed")
    {
        // Draw inline and mark handled automatically
        InspectorHook.DrawProperty(p);
    }
});
```

For custom controls that need keyboard focus and an Enter action:

```csharp
var rect = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
EditorGUI.TextField(rect, "Custom");
if (InspectorFocusHelper.ProcessKeyboardClick(rect))
{
    // Enter pressed while this rect had focus
}
```

### Property and method iteration
- Iterate visible properties of the current object:
  - `InspectorHookUtilities.IterateProperties(prop => { /* ... */ });`
- Recursively walk a property’s children: `InspectorHookUtilities.Iterate(property, onProcess)`
- Iterate declared instance methods on the current type:
  - `InspectorHookUtilities.IterateMethods(method => { /* ... */ });`

### Handling and disabling
- Prevent the default drawer from re‑drawing a property: call `MarkPropertyAsHandled(path)`
- Skip drawing a property entirely: call `MarkPropertyDisabled(path)`; the pipeline will render it disabled if needed
- For compound properties, use `MarkPropertyAndChildrenAsHandled/Disabled`

## Notes and Limitations
- Global scope: applies to all MonoBehaviour inspectors by default; type‑specific CustomEditors still override
- Keep hooks fast: they run for every inspector repaint; avoid heavy reflection per frame
- Selection changes reset state automatically
- Arrays/collections: `Iterate` avoids descending into arrays/collections/dictionaries by default; you can handle them explicitly
- Editor‑only: not included in player builds

## Files in This Package
- `Editor/InspectorHook.cs` – Core hook system (prioritized lists, state, draw helpers, tracking)
- `Editor/InspectorHookUtilities.cs` – Iteration, reflection, attributes, and type helpers
- `Editor/InspectorHookEditor.cs` – Fallback custom editor that drives the pipeline
- `Editor/InspectorFocusHelper.cs` – Keyboard focus and Enter‑key helpers for custom controls
- `package.json` – Package manifest metadata

## Tags
unity, unity-editor, inspector, custom-editor, hook, property, method, attributes, reflection, focus, utility
