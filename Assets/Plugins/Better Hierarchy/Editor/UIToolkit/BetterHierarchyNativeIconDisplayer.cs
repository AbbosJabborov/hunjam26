#if UNITY_6000_5_OR_NEWER
using System.Collections.Generic;
using System.Text;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static BetterHierarchy.BetterHierarchyPreferences;

namespace BetterHierarchy
{
    [InitializeOnLoad]
    public static class BetterHierarchyNativeIconDisplayer
    {
        private sealed class IconState
        {
            public StyleBackground Background;
            public StyleBackgroundSize BackgroundSize;
            public StyleColor Tint;
            public StyleFloat Opacity;
            public string Tooltip;
            public IMGUIContainer Badge;
            public GUIContent Content = new GUIContent();
            public GameObject Object;
            public ScriptIconType Style;
            public Texture2D BaseIcon;
            public StyleBackground AppliedBackground;
            public StyleBackgroundSize AppliedBackgroundSize;
        }

        private static readonly Dictionary<HierarchyViewItem, IconState> ITEMS = new();
        private static readonly HashSet<HierarchyViewItem> BOUND_ITEMS = new();
        private static readonly List<HierarchyViewItem> REFRESH_ITEMS = new();
        private static readonly GUIContent ICON_CONTENT = new();
        private static bool hasDiscoveredItems;
        private static bool isRefreshPending;
        private static bool isRefreshing;

        static BetterHierarchyNativeIconDisplayer()
        {
            HierarchyWindow.BindViewItem += OnBindItem;
            HierarchyWindow.UnbindViewItem += OnUnbindItem;
            HierarchyWindow.GetTooltip += OnGetTooltip;
            OnSettingsChangedEvents += RequestRefresh;
            EditorApplication.hierarchyChanged += RequestRefresh;
            EditorApplication.projectChanged += RequestRefresh;
            Undo.undoRedoPerformed += RequestRefresh;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            RequestRefresh();
        }

        public static void RefreshVisibleItems()
        {
            if (isRefreshing)
            {
                RequestRefresh();
                return;
            }

            EditorApplication.delayCall -= RefreshVisibleItems;
            isRefreshPending = false;
            isRefreshing = true;

            try
            {
                if (!hasDiscoveredItems)
                    DiscoverVisibleItems();

                // Binding callbacks may change the tracked set while a row is being updated.
                REFRESH_ITEMS.AddRange(BOUND_ITEMS);

                foreach (var item in REFRESH_ITEMS)
                {
                    if (!BOUND_ITEMS.Contains(item))
                        continue;

                    if (item.panel == null)
                    {
                        UntrackItem(item);
                        continue;
                    }

                    Apply(item);
                }
            }
            finally
            {
                REFRESH_ITEMS.Clear();
                isRefreshing = false;
            }
        }

        private static void RequestRefresh()
        {
            if (isRefreshPending)
                return;

            isRefreshPending = true;
            EditorApplication.delayCall += RefreshVisibleItems;
        }

        private static void DiscoverVisibleItems()
        {
            hasDiscoveredItems = true;

            // Existing windows need one discovery pass after an assembly reload.
            foreach (var window in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
                window.rootVisualElement.Query<HierarchyViewItem>().ForEach(item => BOUND_ITEMS.Add(item));
        }

        private static void OnObjectChanges(ref ObjectChangeEventStream stream) => RequestRefresh();

        private static void OnBindItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            Restore(item);
            ITEMS.Remove(item);
            BOUND_ITEMS.Add(item);
            Apply(item);
        }

        private static void OnUnbindItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item) => UntrackItem(item);

        private static void UntrackItem(HierarchyViewItem item)
        {
            Restore(item);
            ITEMS.Remove(item);
            BOUND_ITEMS.Remove(item);
        }

        private static void OnGetTooltip(HierarchyWindow window, HierarchyView view,
            HierarchyViewItem item, StringBuilder tooltip, bool filtering)
        {
            if (!IsEnableTooltipsOnHierarchyObject || item.Handler is not HierarchyGameObjectHandler handler)
                return;

            var obj = handler.GetGameObject(item.Node);

            if (!BetterHierarchyIconUtility.TryGetIcon(obj, ICON_CONTENT, out _) ||
                string.IsNullOrEmpty(ICON_CONTENT.tooltip))
                return;

            // Keep Unity's own row information, including paths in filtered views.
            if (tooltip.Length > 0)
                tooltip.AppendLine();

            tooltip.Append(ICON_CONTENT.tooltip);
        }

        private static void Apply(HierarchyViewItem item)
        {
            if (item.Handler is not HierarchyGameObjectHandler handler ||
                !BetterHierarchyIconUtility.TryGetIcon(handler.GetGameObject(item.Node), ICON_CONTENT, out var style))
            {
                Restore(item);
                ITEMS.Remove(item);
                return;
            }

            var obj = handler.GetGameObject(item.Node);
            var baseIcon = style == ScriptIconType.SmallIcon ? PrefabUtility.GetIconForGameObject(obj) : null;
            float opacity = obj.activeInHierarchy ? 1f : 0.5f;

            if (ITEMS.TryGetValue(item, out var state))
            {
                if (HasSameAppearance(item, state, obj, style, baseIcon, opacity))
                    return;

                RestoreOriginalAppearance(item, state);
            }
            else
                state = SaveOriginalIcon(item);

            UpdateIcon(item, state, obj, style, baseIcon, opacity);
        }

        private static bool HasSameAppearance(HierarchyViewItem item, IconState state, GameObject obj,
            ScriptIconType style, Texture2D baseIcon, float opacity)
        {
            bool hasExpectedBadge = style == ScriptIconType.SmallIcon
                ? state.Badge != null && state.Badge.parent == item.Icon && state.Badge.tooltip == ICON_CONTENT.tooltip
                : state.Badge == null || state.Badge.parent == null;

            return state.Object == obj && state.Style == style && state.BaseIcon == baseIcon &&
                state.Content.image == ICON_CONTENT.image && state.Content.tooltip == ICON_CONTENT.tooltip &&
                item.Icon.style.backgroundImage.Equals(state.AppliedBackground) &&
                item.Icon.style.backgroundSize.Equals(state.AppliedBackgroundSize) &&
                item.Icon.style.unityBackgroundImageTintColor.value == Color.white &&
                item.Icon.style.opacity.value == opacity && item.Icon.tooltip == ICON_CONTENT.tooltip && hasExpectedBadge;
        }

        private static IconState SaveOriginalIcon(HierarchyViewItem item)
        {
            var state = new IconState
            {
                Background = item.Icon.style.backgroundImage,
                BackgroundSize = item.Icon.style.backgroundSize,
                Tint = item.Icon.style.unityBackgroundImageTintColor,
                Opacity = item.Icon.style.opacity,
                Tooltip = item.Icon.tooltip
            };

            ITEMS.Add(item, state);
            return state;
        }

        private static void UpdateIcon(HierarchyViewItem item, IconState state, GameObject obj,
            ScriptIconType style, Texture2D baseIcon, float opacity)
        {
            state.Object = obj;
            state.Style = style;
            state.BaseIcon = baseIcon;
            state.Content.image = ICON_CONTENT.image;
            state.Content.tooltip = ICON_CONTENT.tooltip;
            item.Icon.style.unityBackgroundImageTintColor = Color.white;
            item.Icon.style.opacity = opacity;
            item.Icon.tooltip = state.Content.tooltip;

            if (style == ScriptIconType.BigIcon)
            {
                state.Badge?.RemoveFromHierarchy();
                item.Icon.style.backgroundImage = Background.FromTexture2D((Texture2D)state.Content.image);
            }
            else
                ApplyComponentBadge(item, state);

            state.AppliedBackground = item.Icon.style.backgroundImage;
            state.AppliedBackgroundSize = item.Icon.style.backgroundSize;
        }

        private static void ApplyComponentBadge(HierarchyViewItem item, IconState state)
        {
            // Use the IMGUI Hierarchy's cube, including prefab variants and ordinary children.
            item.Icon.style.backgroundImage = Background.FromTexture2D(state.BaseIcon);
            item.Icon.style.backgroundSize = new BackgroundSize(16, 16);
            state.Badge ??= CreateComponentBadge(state);
            state.Badge.tooltip = state.Content.tooltip;

            if (state.Badge.parent != item.Icon)
                item.Icon.Add(state.Badge);

            state.Badge.MarkDirtyRepaint();
        }

        private static IMGUIContainer CreateComponentBadge(IconState state)
        {
            // Keep custom component icons and the same badge renderer as the IMGUI Hierarchy.
            var badge = new IMGUIContainer(() => EditorGUI.LabelField(new Rect(0, 0, 10, 10), state.Content))
            {
                name = "better-hierarchy-component-badge",
                pickingMode = PickingMode.Ignore
            };

            badge.style.position = Position.Absolute;
            badge.style.left = 7;
            badge.style.top = 7;
            badge.style.width = 10;
            badge.style.height = 10;
            return badge;
        }

        private static void Restore(HierarchyViewItem item)
        {
            if (!ITEMS.TryGetValue(item, out var state))
                return;

            state.Badge?.RemoveFromHierarchy();
            RestoreOriginalAppearance(item, state);
        }

        private static void RestoreOriginalAppearance(HierarchyViewItem item, IconState state)
        {
            item.Icon.style.backgroundImage = state.Background;
            item.Icon.style.backgroundSize = state.BackgroundSize;
            item.Icon.style.unityBackgroundImageTintColor = state.Tint;
            item.Icon.style.opacity = state.Opacity;
            item.Icon.tooltip = state.Tooltip;
        }

        private static void Shutdown()
        {
            HierarchyWindow.BindViewItem -= OnBindItem;
            HierarchyWindow.UnbindViewItem -= OnUnbindItem;
            HierarchyWindow.GetTooltip -= OnGetTooltip;
            OnSettingsChangedEvents -= RequestRefresh;
            EditorApplication.hierarchyChanged -= RequestRefresh;
            EditorApplication.projectChanged -= RequestRefresh;
            Undo.undoRedoPerformed -= RequestRefresh;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            EditorApplication.delayCall -= RefreshVisibleItems;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;

            foreach (var item in ITEMS.Keys)
                Restore(item);

            ITEMS.Clear();
            BOUND_ITEMS.Clear();
            REFRESH_ITEMS.Clear();
        }
    }
}
#endif
