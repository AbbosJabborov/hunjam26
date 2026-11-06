using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterHierarchy
{
    // Older installations may retain the previous private helper scripts on update.
    // Keep this type partial and retain the shared label width for those scripts.
    public static partial class BetterHierarchyPreferences
    {
        public enum ScriptIconType
        {
            SmallIcon,
            BigIcon,
            UnityDefault
        }

        public enum UnityNativeScriptsDetectionType
        {
            UnityEngine,
            Unity,
            None
        }

        public const string TITLE = "Better Hierarchy";
        private const string PREFIX = "BetterHierarchy.";
        private const float MIN_LABEL_WIDTH = 230;

        // Preserve the stored keys so updates retain existing user preferences.
        private const string ENABLED_KEY = PREFIX + "isEnabledBetterHierarchy";
        private const string ALWAYS_FIRST_KEY = PREFIX + "showAlwaysFirstScriptIcon";
        private const string OVERRIDE_PREFABS_KEY = PREFIX + "isOverridePrefabIconType";
        private const string TOOLTIPS_KEY = PREFIX + "isEnableTooltipsOnHierarchyObject";
        private const string NAMESPACE_KEYWORD_KEY = PREFIX + "unityScriptDetectionType";
        private const string UNITY_SCRIPTS_KEY = PREFIX + "containsUnityScriptsOnly";
        private const string USER_SCRIPTS_KEY = PREFIX + "containsNonUnityScripts";
        private const string SINGLE_SCRIPT_KEY = PREFIX + "containsSingleUserScript";
        private const string NO_SCRIPTS_KEY = PREFIX + "containsNoScripts";
        private const string PREFABS_KEY = PREFIX + "isAPrefab";

        public static bool IsEnabled => EditorPrefs.GetBool(ENABLED_KEY, true);
        public static bool IsShowAlwaysFirstScriptIcon => EditorPrefs.GetBool(ALWAYS_FIRST_KEY, false);
        public static bool IsOverridePrefabIcons => EditorPrefs.GetBool(OVERRIDE_PREFABS_KEY, false);
        public static bool IsEnableTooltipsOnHierarchyObject => EditorPrefs.GetBool(TOOLTIPS_KEY, true);

        public static UnityNativeScriptsDetectionType UnityScriptDetectionType =>
            (UnityNativeScriptsDetectionType)EditorPrefs.GetInt(NAMESPACE_KEYWORD_KEY, (int)UnityNativeScriptsDetectionType.Unity);

        public static ScriptIconType ContainsOnlyUnityScriptsType => GetStyle(UNITY_SCRIPTS_KEY, ScriptIconType.BigIcon);
        public static ScriptIconType ContainsNonUnityScriptsType => GetStyle(USER_SCRIPTS_KEY, ScriptIconType.SmallIcon);
        public static ScriptIconType ContainsSingleScriptType => GetStyle(SINGLE_SCRIPT_KEY, ScriptIconType.SmallIcon);
        public static ScriptIconType ContainsNoScriptsType => GetStyle(NO_SCRIPTS_KEY, ScriptIconType.BigIcon);
        public static ScriptIconType OverridenPrefabIcons => GetStyle(PREFABS_KEY, ScriptIconType.SmallIcon);

        public static event System.Action OnSettingsChangedEvents = delegate { };

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider() =>
            new SettingsProvider("Preferences/BetterHierarchy", SettingsScope.User)
            {
                label = TITLE,
                keywords = new HashSet<string> { "Hierarchy", "Icons", "Components", "Prefab", "Tooltips" },
                activateHandler = (_, root) => CreatePreferencesPanel(root)
            };

        private static ScriptIconType GetStyle(string key, ScriptIconType fallback)
        {
            int value = EditorPrefs.GetInt(key, (int)fallback);
            return value >= 0 && value <= (int)ScriptIconType.UnityDefault ? (ScriptIconType)value : fallback;
        }

        private static void CreatePreferencesPanel(VisualElement root)
        {
            var panel = CreatePanel(root);
            AddToggle(panel, ENABLED_KEY, "Enabled", IsEnabled, "Customize component icons in the IMGUI and UI Toolkit Hierarchy windows.");
            AddComponentPreferences(panel);
            AddPrefabPreferences(panel);
            AddTooltipPreference(panel);
        }

        private static VisualElement CreatePanel(VisualElement root)
        {
            var panel = new VisualElement();
            panel.style.marginLeft = 8;
            panel.style.marginRight = 8;
            panel.style.marginTop = 2;
            root.Add(panel);

#if UNITY_6000_3_OR_NEWER && !UNITY_6000_5_OR_NEWER
            panel.Add(new Label("This Unity version previews the new Hierarchy without public customization APIs. Turn off Use new Hierarchy window in Preferences > General to show Better Hierarchy icons."));
#endif
            var header = new Label(TITLE);
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            panel.Add(header);
            return panel;
        }

        private static void AddComponentPreferences(VisualElement panel)
        {
            AddStyle(panel, SINGLE_SCRIPT_KEY, "Contains Single User Script Only", ContainsSingleScriptType);
            AddStyle(panel, NO_SCRIPTS_KEY, "Contains No Scripts", ContainsNoScriptsType);
            var unityStyle = AddStyle(panel, UNITY_SCRIPTS_KEY, "Contains Unity Scripts Only", ContainsOnlyUnityScriptsType);

            var first = AddToggle(panel, ALWAYS_FIRST_KEY, "Always Show First Script Icon", IsShowAlwaysFirstScriptIcon,
                "Use the first component after Transform instead of searching for a user script.");

            first.style.marginTop = 15;
            var userStyle = AddStyle(panel, USER_SCRIPTS_KEY, "Contains Non-Unity Scripts", ContainsNonUnityScriptsType);
            AddNamespacePreference(panel);

            void UpdateDetectionFields()
            {
                unityStyle.label = first.value ? "First Script Is Unity Related" : "Contains Unity Scripts Only";
                userStyle.SetEnabled(!first.value);
            }

            first.RegisterValueChangedCallback(_ => UpdateDetectionFields());
            UpdateDetectionFields();
        }

        private static void AddNamespacePreference(VisualElement panel)
        {
            var keyword = new EnumField("Unity Native Script Keyword", UnityScriptDetectionType);
            keyword.tooltip = "Unity matches namespaces containing Unity; UnityEngine matches UnityEngine and UnityEditor namespaces; None treats all components as user scripts. TextMesh Pro remains a built-in component.";
            panel.Add(keyword);

            keyword.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetInt(NAMESPACE_KEYWORD_KEY, (int)(UnityNativeScriptsDetectionType)evt.newValue);
                OnSettingsChangedEvents?.Invoke();
            });
        }

        private static void AddPrefabPreferences(VisualElement panel)
        {
            var overridePrefabs = AddToggle(panel, OVERRIDE_PREFABS_KEY, "Override Prefab Icons",
                IsOverridePrefabIcons, "Use a separate icon style for prefabs and their children.");

            overridePrefabs.style.marginTop = 15;
            var prefabStyle = AddStyle(panel, PREFABS_KEY, "Is A Prefab", OverridenPrefabIcons);
            prefabStyle.SetEnabled(overridePrefabs.value);
            overridePrefabs.RegisterValueChangedCallback(evt => prefabStyle.SetEnabled(evt.newValue));
        }

        private static void AddTooltipPreference(VisualElement panel)
        {
            var tooltips = AddToggle(panel, TOOLTIPS_KEY, "Enable Component Tooltips", IsEnableTooltipsOnHierarchyObject,
                "Show the selected component type over its icon and object name. In the UI Toolkit Hierarchy, Unity's own row information is also preserved.");

            tooltips.style.marginTop = 15;
        }

        private static Toggle AddToggle(VisualElement parent, string key, string label, bool value, string tooltip)
        {
            var field = new Toggle(label) { value = value, tooltip = tooltip };
            field.labelElement.style.minWidth = MIN_LABEL_WIDTH;

            field.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(key, evt.newValue);
                OnSettingsChangedEvents?.Invoke();
            });

            parent.Add(field);
            return field;
        }

        private static EnumField AddStyle(VisualElement parent, string key, string label, ScriptIconType value)
        {
            var field = new EnumField(label, value);
            field.labelElement.style.minWidth = MIN_LABEL_WIDTH;
            field.tooltip = "Small Icon adds a component badge; Big Icon replaces the row icon; Unity Default keeps Unity's icon.";

            field.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetInt(key, (int)(ScriptIconType)evt.newValue);
                OnSettingsChangedEvents?.Invoke();
            });

            parent.Add(field);
            return field;
        }
    }
}
