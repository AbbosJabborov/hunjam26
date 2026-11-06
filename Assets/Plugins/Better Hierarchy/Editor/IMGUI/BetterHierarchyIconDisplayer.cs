using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BetterHierarchy.BetterHierarchyPreferences;
#if UNITY_6000_5_OR_NEWER
using HierarchyObjectId = UnityEngine.EntityId;
#else
using HierarchyObjectId = System.Int32;
#endif

namespace BetterHierarchy
{
    [InitializeOnLoad]
    public static class BetterHierarchyIconDisplayer
    {
        private static bool hierarchyHasFocus;
        private static int selectedObjectCount;
        private static bool isTrackingHierarchy;
        private static readonly List<EditorWindow> HIERARCHY_WINDOWS = new List<EditorWindow>();
        private static readonly HashSet<HierarchyObjectId> ADDITIONAL_SELECTED_OBJECT_IDS = new HashSet<HierarchyObjectId>();
        private static readonly HashSet<HierarchyObjectId> SELECTED_OBJECT_IDS = new HashSet<HierarchyObjectId>();
        private static readonly GUIContent ICON_CONTENT = new GUIContent();
        private static readonly GUIContent TOOLTIP_CONTENT = new GUIContent();
        private const float HIERARCHY_ICON_WIDTH = 18.5f;

        static BetterHierarchyIconDisplayer()
        {
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
#endif
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            OnSettingsChangedEvents += OnSettingsChanged;
        }

        private static bool StartBookkeeping()
        {
            if (isTrackingHierarchy)
                return true;

            // Discover windows only when the IMGUI Hierarchy actually draws a row.
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().Name == "SceneHierarchyWindow")
                    HIERARCHY_WINDOWS.Add(window);

            if (HIERARCHY_WINDOWS.Count == 0)
                return false;

            isTrackingHierarchy = true;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += UpdateSelection;
            hierarchyHasFocus = IsHierarchyWindowFocused();
            UpdateSelection();
            return true;
        }

        private static void StopBookkeeping()
        {
            if (!isTrackingHierarchy)
                return;

            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= UpdateSelection;
            isTrackingHierarchy = false;
            hierarchyHasFocus = false;
            selectedObjectCount = 0;
            HIERARCHY_WINDOWS.Clear();
            ADDITIONAL_SELECTED_OBJECT_IDS.Clear();
            SELECTED_OBJECT_IDS.Clear();
        }

        private static void OnSettingsChanged()
        {
            if (!IsEnabled)
                StopBookkeeping();

            // A repaint wakes an open IMGUI Hierarchy when the plugin is re-enabled.
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnEditorUpdate()
        {
            for (int i = HIERARCHY_WINDOWS.Count - 1; i >= 0; i--)
                if (!HIERARCHY_WINDOWS[i])
                    HIERARCHY_WINDOWS.RemoveAt(i);

            if (!IsEnabled || HIERARCHY_WINDOWS.Count == 0)
            {
                StopBookkeeping();
                return;
            }

            hierarchyHasFocus = IsHierarchyWindowFocused();
            ADDITIONAL_SELECTED_OBJECT_IDS.Clear();
        }

        private static bool IsHierarchyWindowFocused()
        {
            var focusedWindow = EditorWindow.focusedWindow;

            return focusedWindow != null && focusedWindow.GetType().Name == "SceneHierarchyWindow";
        }

        private static void UpdateSelection()
        {
            SELECTED_OBJECT_IDS.Clear();
#if UNITY_6000_5_OR_NEWER
            var selectedIds = Selection.entityIds;
#else
            var selectedIds = Selection.instanceIDs;
#endif
            selectedObjectCount = selectedIds.Length;

            foreach (var id in selectedIds)
                SELECTED_OBJECT_IDS.Add(id);
        }

        private static void OnHierarchyWindowItemOnGUI(HierarchyObjectId instanceID, Rect selectionRect)
        {
            if (!IsEnabled || !StartBookkeeping())
                return;

#if UNITY_6000_5_OR_NEWER
            GameObject obj = EditorUtility.EntityIdToObject(instanceID) as GameObject;
#else
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
#endif

            if (!obj)
                return;

            if (!BetterHierarchyIconUtility.TryGetIcon(obj, ICON_CONTENT, out var iconType))
                return;

            var objectStatus = GetHierarchyObjectStatus(instanceID, selectionRect);
            UpdateSelectedObjectsList(instanceID, objectStatus, selectedObjectCount);

            if (iconType == ScriptIconType.BigIcon)
                ClearOriginalIcon(objectStatus, selectionRect, selectedObjectCount);

            DrawIcon(selectionRect, ICON_CONTENT, obj, iconType);
        }

        private static HierarchyObjectStatus GetHierarchyObjectStatus(HierarchyObjectId instanceID, Rect selectionRect)
        {
            const float HIERARCHY_EXPAND_ICON_WIDTH = 11f;
            const float HIERARCHY_EXPAND_ICON_X_OFFSET = HIERARCHY_EXPAND_ICON_WIDTH + 3f;

            Rect entireRowRect = selectionRect;
            entireRowRect.x = 0;
            entireRowRect.width = short.MaxValue;
            Rect expandChildrenIconRect = selectionRect;
            expandChildrenIconRect.x -= HIERARCHY_EXPAND_ICON_X_OFFSET;
            expandChildrenIconRect.width = HIERARCHY_EXPAND_ICON_WIDTH;

            return new HierarchyObjectStatus
            {
                IsSelected = SELECTED_OBJECT_IDS.Contains(instanceID),
                IsHovered = entireRowRect.Contains(Event.current.mousePosition),
                IsDropDownHovered = expandChildrenIconRect.Contains(Event.current.mousePosition)
            };
        }

        private static void UpdateSelectedObjectsList(HierarchyObjectId instanceID, HierarchyObjectStatus objectStatus, int selectedCount)
        {
            if (objectStatus.IsSelected || (objectStatus.IsDropDownHovered && MouseStatus.IsMouseDown))
            {
                if (selectedCount > 1)
                    ADDITIONAL_SELECTED_OBJECT_IDS.Clear();

                ADDITIONAL_SELECTED_OBJECT_IDS.Add(instanceID);
            }
            else
                ADDITIONAL_SELECTED_OBJECT_IDS.Remove(instanceID);
        }

        private static void DrawIcon(Rect selectionRect, GUIContent objContent, GameObject obj, ScriptIconType iconType = ScriptIconType.BigIcon)
        {
            Rect tooltipRect = selectionRect;
            if (iconType == ScriptIconType.SmallIcon)
            {
                selectionRect.width = 10;
                selectionRect.height = 10;
                selectionRect.position += new Vector2(7, 7);
            }

            Color originalColor = GUI.color;

            if (!obj.activeInHierarchy)
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

            EditorGUI.LabelField(selectionRect, objContent);
            GUI.color = originalColor;

            // Keep the component tooltip reachable over the name as well as a small badge.
            if (!string.IsNullOrEmpty(objContent.tooltip))
            {
                TOOLTIP_CONTENT.tooltip = objContent.tooltip;
                GUI.Label(tooltipRect, TOOLTIP_CONTENT, GUIStyle.none);
            }
        }

        private static void ClearOriginalIcon(HierarchyObjectStatus hierarchyObjectStatus, Rect selectionRect, int selectedCount)
        {
            int selectedAmount = selectedCount > 1 ? selectedCount : ADDITIONAL_SELECTED_OBJECT_IDS.Count;
            var color = UnityEditorBackgroundColor.Get(hierarchyObjectStatus, hierarchyHasFocus, selectedAmount);
            Rect backgroundRect = selectionRect;
            backgroundRect.width = HIERARCHY_ICON_WIDTH;
            EditorGUI.DrawRect(backgroundRect, color);
        }

        private static void Shutdown()
        {
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemOnGUI;
#endif
            StopBookkeeping();
            OnSettingsChangedEvents -= OnSettingsChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        }
    }
}
