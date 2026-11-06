# Changelog

## Unreleased

### Added

- Support for the UI Toolkit Hierarchy in Unity 6.5 and 6.6, alongside the IMGUI
  Hierarchy in Unity 2022.3 and later.
- Component type tooltips on object names in the UI Toolkit Hierarchy.
- Demo prefabs and a linked prefab variant, with nested children, mixed scripts,
  inactive objects, and scene overrides for comparing preference settings.

### Fixed

- Small Icon in the UI Toolkit Hierarchy now uses the same full-size GameObject,
  prefab, and prefab variant cubes as the IMGUI Hierarchy.
- Namespace detection now works with custom component icons and generic script
  icons, including when **Always Show First Script Icon** is enabled.
- Disabling Better Hierarchy or choosing **Unity Default** restores the original
  icons in the UI Toolkit Hierarchy.
- Icons in the UI Toolkit Hierarchy refresh after component changes, asset changes, and
  Undo/Redo.
- Component tooltips now appear over object names in the IMGUI Hierarchy, including
  when Small Icon is selected.
- The Single User Script demo object now contains only its Transform and user script.
- Children added to a prefab instance in the scene now follow **Override Prefab
  Icons**, like the prefab's existing children.

### Changed

- IMGUI selection and background-state updates now pause when its Hierarchy
  windows are closed or Better Hierarchy is disabled.
- Reduced repeated refresh work in the UI Toolkit Hierarchy and avoidable
  allocations in both Hierarchy implementations.
- Organized Editor scripts into Shared, Preferences, IMGUI, and UIToolkit folders
  for easier navigation and customization.

### Updating from an earlier release

Existing icon preferences are retained. The plugin's C# namespace changed from
`Utilities.BetterHierarchy` to `BetterHierarchy`. If your own Editor scripts
reference the old namespace, update their `using` directives or qualified names.

Unity 6.3 and 6.4 require the IMGUI Hierarchy window. See the
[README](README.md#unity-compatibility) for setup instructions and compatibility
details.
