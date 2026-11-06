# Better Hierarchy Documentation

Better Hierarchy displays component icons next to GameObjects in Unity's
Hierarchy, so you can identify objects without selecting them. Choose a small
component badge, a full-size component icon, or Unity's default icon for different
types of objects.

## Table of contents

1. [Getting started](#getting-started)
2. [Unity compatibility](#unity-compatibility)
3. [Choosing icon styles](#choosing-icon-styles)
4. [How scripts are classified](#how-scripts-are-classified)
5. [Tooltips](#tooltips)
6. [Demo scene](#demo-scene)
7. [Updating an existing installation](#updating-an-existing-installation)
8. [If icons are not appearing](#if-icons-are-not-appearing)

## Getting started

1. Import Better Hierarchy into your project and let Unity finish compiling.
2. Open Unity's **Preferences** and select **Better Hierarchy**.
3. Make sure **Enabled** is checked, then choose your icon styles.

Better Hierarchy works automatically; you do not need to add components to your
objects. It runs in the Editor and is excluded from player builds.

## Unity compatibility

| Unity version | Hierarchy window |
| --- | --- |
| 2022.3 through 6.2 | IMGUI Hierarchy |
| 6.3 and 6.4 | IMGUI Hierarchy required |
| 6.5 and 6.6 | UI Toolkit Hierarchy; IMGUI Hierarchy also supported where available |

**Unity 6.3 and 6.4:** open **Preferences > General** and turn off **Use new
Hierarchy window**. Those versions preview the new window without the public
customization support that Better Hierarchy needs.

Editor behavior has been tested in Unity 2022.3.62f2 and 6000.6.0f1, including
entering Play Mode with Domain Reload disabled. Compilation has also been checked
in 6000.2.6f2. Unity 6.3, 6.4, and 6.5 have not been individually tested. Versions
earlier than 2022.3 and later than 6.6 have not been validated.

## Choosing icon styles

| Style | Appearance |
| --- | --- |
| **Small Icon** | A component badge on the GameObject or prefab cube. |
| **Big Icon** | The selected component's icon replaces the normal row icon. |
| **Unity Default** | Unity's original icon, without customization. |

Custom component icons are used when available. Empty objects use their Transform
icon, and icons on inactive objects are dimmed.

The following preferences let you choose a style for each kind of object:

| Preference | Applies to |
| --- | --- |
| **Contains Single User Script Only** | An object with one user component after its Transform. |
| **Contains No Scripts** | An object with only a Transform. |
| **Contains Unity Scripts Only** | An object whose components are all classified as built-in. |
| **Contains Non-Unity Scripts** | An object with several components, including a user script. |

By default, Better Hierarchy looks for a user script to represent an object.
Enable **Always Show First Script Icon** to use the first component after
Transform instead. In this mode, **First Script Is Unity Related** controls the
style for a built-in component; **Contains Single User Script Only** controls the
style for a user component.

For objects with more than 10 components, including Transform, the automatic
user-script search uses a generic component icon. Enable **Always Show First
Script Icon** if you prefer to show the first component directly.

Enable **Override Prefab Icons** to give prefab objects and their children a
separate style using **Is A Prefab**.

## How scripts are classified

**Unity Native Script Keyword** determines whether a component is treated as
built-in or as a user script. Classification uses its C# namespace, independently
of its icon.

| Keyword | Components treated as built-in |
| --- | --- |
| **Unity** | Any namespace containing `Unity`, including custom namespaces such as `MyUnityTools`. |
| **UnityEngine** | The `UnityEngine` and `UnityEditor` namespaces and their subnamespaces. |
| **None** | No components are classified as built-in by namespace. |

TextMesh Pro components are always treated as built-in. Namespace matching is
case-sensitive. These settings also apply when **Always Show First Script Icon**
is enabled.

## Tooltips

Enable **Enable Component Tooltips** to show the selected component's type when
hovering over its icon or object name in either Hierarchy window. In the UI Toolkit
Hierarchy, the tooltip also preserves Unity's own object information. The option
applies to rows customized by Better Hierarchy; **Unity Default** keeps Unity's
own tooltip.

## Demo scene

Open **Assets/Plugins/Better Hierarchy/Demo Scene/Better Hierarchy Demo Scene** to
try the settings on built-in components, user scripts, UI objects, and inactive
objects.

To try **Unity Native Script Keyword**, find **Custom Unity Namespace Included
Script** in the demo. Its script uses `BetterHierarchy.UnityNamespaceExample`:
choosing **Unity** treats it as built-in, while **UnityEngine** and **None** treat
it as a user script. Set **Contains Unity Scripts Only** and **Contains Non-Unity
Scripts** to different icon styles to make the difference visible.

Expand **Prefab and Variant Examples** to compare:

- **Prefab - Base:** children with only built-in components, only a user script,
  mixed components in different orders, a Unity-containing namespace, and an
  inactive child. The Grandchildren group includes another level of nesting.
- **Prefab Variant - Child Overrides:** a variant linked to the base prefab, with
  an added user component, an activated child, and an additional child.
- **Prefab Instance - Scene Overrides:** an instance of the base prefab with a
  component override and an extra child added only in the scene.

With **Override Prefab Icons** off, change the built-in and user-script styles to
compare component detection across these objects. Turn it on and change **Is A
Prefab** to compare prefab roots, children, and variant objects. Use **Unity
Default** to see Unity's own prefab and override indicators. **Always Show First
Script Icon** lets you compare the two mixed-component children with different
component orders.

The demo requires Unity UI and TextMesh Pro:

- In Unity 2022.3, install **Unity UI** and **TextMesh Pro** through Package Manager
  if they are not already installed.
- In Unity 6.6, TextMesh Pro is included in the **Unity UI** package.
- If you have not imported TMP's essential resources, select **Window >
  TextMeshPro > Import TMP Essential Resources** before opening the demo.

These dependencies are needed only for the demo. Better Hierarchy itself requires
no additional packages.

## Updating an existing installation

Import the update into the existing **Assets/Plugins/Better Hierarchy** folder.
Your icon preferences are retained.

If your own Editor scripts reference `Utilities.BetterHierarchy`, change those
references to `BetterHierarchy`. For example:

```csharp
using BetterHierarchy;
```

No changes are needed to scene objects to use the updated plugin.

## If icons are not appearing

- Check **Preferences > Better Hierarchy > Enabled**.
- Check that the applicable icon style is not set to **Unity Default**.
- On Unity 6.3 or 6.4, switch to the IMGUI Hierarchy as described above.
- Wait for Unity to finish compiling and resolve any script compilation errors
  shown in the Console.
