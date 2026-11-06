using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BetterHierarchy.BetterHierarchyPreferences;

namespace BetterHierarchy
{
    public static class BetterHierarchyIconUtility
    {
        private const int MAX_AUTOMATIC_COMPONENT_COUNT = 10;
        private static readonly List<Component> COMPONENTS = new List<Component>(16);

        public static bool TryGetIcon(GameObject obj, out GUIContent content, out ScriptIconType iconType)
        {
            content = null;

            if (!TryGetComponent(obj, out var component, out iconType))
                return false;

            content = new GUIContent();
            UpdateContent(content, component);
            return iconType != ScriptIconType.UnityDefault && content.image;
        }

        internal static bool TryGetIcon(GameObject obj, GUIContent content, out ScriptIconType iconType)
        {
            if (!TryGetComponent(obj, out var component, out iconType))
                return false;

            UpdateContent(content, component);
            return iconType != ScriptIconType.UnityDefault && content.image;
        }

        internal static bool IsPartOfPrefabHierarchy(GameObject obj)
        {
            // Scene-added children have no prefab asset link of their own.
            for (Transform current = obj ? obj.transform : null; current; current = current.parent)
                if (PrefabUtility.IsPartOfAnyPrefab(current.gameObject))
                    return true;

            return false;
        }

        private static bool TryGetComponent(GameObject obj, out Component component, out ScriptIconType iconType)
        {
            component = null;
            iconType = ScriptIconType.UnityDefault;

            if (!IsEnabled || !obj)
                return false;

            try
            {
                obj.GetComponents(COMPONENTS);

                if (COMPONENTS.Count == 0)
                    return false;

                component = COMPONENTS.Count > 1 ? COMPONENTS[1] : COMPONENTS[0];

                if (!IsShowAlwaysFirstScriptIcon && component is CanvasRenderer)
                    component = COMPONENTS[COMPONENTS.Count - 1];

                if (COMPONENTS.Count == 1)
                    iconType = ContainsNoScriptsType;
                else if (COMPONENTS.Count == 2 || IsShowAlwaysFirstScriptIcon)
                    iconType = IsNamespaceUnityRelated(component) ? ContainsOnlyUnityScriptsType : ContainsSingleScriptType;
                else if (HasCustomScripts(COMPONENTS, out var customComponent))
                {
                    component = customComponent;
                    iconType = ContainsNonUnityScriptsType;
                }
                else
                    iconType = ContainsOnlyUnityScriptsType;
            }
            finally
            {
                // Release scene references before ObjectContent can enter other Editor callbacks.
                COMPONENTS.Clear();
            }

            if (IsOverridePrefabIcons && IsPartOfPrefabHierarchy(obj))
                iconType = OverridenPrefabIcons;

            return true;
        }

        private static void UpdateContent(GUIContent content, Component component)
        {
            var type = component ? component.GetType() : typeof(Component);
            // ObjectContent can return shared GUIContent: never mutate Unity's cached content.
            content.image = EditorGUIUtility.ObjectContent(component, type).image;
            content.text = null;
            content.tooltip = IsEnableTooltipsOnHierarchyObject ? type.Name : "";
        }

        private static bool HasCustomScripts(IReadOnlyList<Component> components, out Component component)
        {
            component = null;

            if (components.Count > MAX_AUTOMATIC_COMPONENT_COUNT)
                return true;

            for (int i = 1; i < components.Count; i++)
            {
                if (IsNamespaceUnityRelated(components[i]))
                    continue;

                component = components[i];
                return true;
            }

            return false;
        }

        private static bool IsNamespaceUnityRelated(Component component)
        {
            if (!component)
                return false;

            string componentNamespace = component.GetType().Namespace ?? "";

            if (componentNamespace.StartsWith("TMPro"))
                return true;

            switch (UnityScriptDetectionType)
            {
                case UnityNativeScriptsDetectionType.Unity:
                    return componentNamespace.Contains("Unity");
                case UnityNativeScriptsDetectionType.UnityEngine:

                    return componentNamespace == "UnityEngine" || componentNamespace == "UnityEditor" ||
                        componentNamespace.StartsWith("UnityEngine.") || componentNamespace.StartsWith("UnityEditor.");

                default:
                    return false;
            }
        }
    }
}
