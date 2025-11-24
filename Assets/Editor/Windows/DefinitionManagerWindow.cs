using System;
using System.Collections.Generic;
using System.Linq;
using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Windows
{
    public class DefinitionManagerWindow : EditorWindow
    {
        private const string MenuPath = "Adventure/Definition Manager";

        private readonly Dictionary<UnityEngine.Object, bool> foldoutStates = new Dictionary<UnityEngine.Object, bool>();
        private readonly List<DefinitionEntry<AbilityDefinition>> abilityEntries = new List<DefinitionEntry<AbilityDefinition>>();
        private readonly List<DefinitionEntry<ClassDefinition>> classEntries = new List<DefinitionEntry<ClassDefinition>>();

        private Vector2 scroll;
        private string searchTerm = string.Empty;
        private int tagFilterIndex;
        private List<string> tagOptions = new List<string> {"All"};

        private Sprite defaultIcon;
        private string duplicateSuffix = "_Copy";
        private StatBlock statPreset;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            DefinitionManagerWindow window = GetWindow<DefinitionManagerWindow>();
            window.titleContent = new GUIContent("Definition Manager");
            window.RefreshData();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshData();
        }

        private void RefreshData()
        {
            abilityEntries.Clear();
            classEntries.Clear();
            tagOptions = new List<string> {"All"};

            string[] abilityGuids = AssetDatabase.FindAssets("t:AbilityDefinition");
            foreach (string guid in abilityGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AbilityDefinition asset = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (asset != null)
                {
                    abilityEntries.Add(new DefinitionEntry<AbilityDefinition>(asset));
                    AddTags(asset.Tags);
                }
            }

            string[] classGuids = AssetDatabase.FindAssets("t:ClassDefinition");
            foreach (string guid in classGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ClassDefinition asset = AssetDatabase.LoadAssetAtPath<ClassDefinition>(path);
                if (asset != null)
                {
                    classEntries.Add(new DefinitionEntry<ClassDefinition>(asset));
                    AddTags(asset.Tags);
                }
            }

            abilityEntries.Sort((a, b) => string.Compare(a.Asset.DisplayName, b.Asset.DisplayName, StringComparison.OrdinalIgnoreCase));
            classEntries.Sort((a, b) => string.Compare(a.Asset.DisplayName, b.Asset.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();
            DrawBulkOperations();
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawDefinitionList("Classes", classEntries, DrawClassFields);
            EditorGUILayout.Space();
            DrawDefinitionList("Abilities", abilityEntries, DrawAbilityFields);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawValidationPanel();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.textField;
            searchTerm = GUILayout.TextField(searchTerm, searchStyle, GUILayout.Width(250));
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                searchTerm = string.Empty;
                GUI.FocusControl(null);
            }

            int newIndex = EditorGUILayout.Popup(tagFilterIndex, tagOptions.ToArray(), GUILayout.Width(160));
            if (newIndex != tagFilterIndex)
            {
                tagFilterIndex = newIndex;
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshData();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBulkOperations()
        {
            EditorGUILayout.LabelField("Bulk Operations", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            duplicateSuffix = EditorGUILayout.TextField(new GUIContent("Duplicate Suffix", "Appended to ID and display name when duplicating assets."), duplicateSuffix);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate Selected"))
            {
                DuplicateSelected();
            }
            if (GUILayout.Button("Fix Broken References"))
            {
                FixBrokenReferences();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            defaultIcon = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Default Icon", "Assign to selected assets missing an icon."), defaultIcon, typeof(Sprite), false);
            if (GUILayout.Button("Assign Default Icons"))
            {
                AssignDefaultIcons();
            }

            EditorGUILayout.Space();

            statPreset = (StatBlock)EditorGUILayout.ObjectField(new GUIContent("Stat Preset", "Applied to selected class definitions."), statPreset, typeof(StatBlock), false);
            if (GUILayout.Button("Apply Stat Preset to Selected Classes"))
            {
                ApplyStatPreset();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDefinitionList<T>(string header, List<DefinitionEntry<T>> entries, Action<SerializedObject> drawFields) where T : ScriptableObject, IIdentifiableDefinition
        {
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            foreach (DefinitionEntry<T> entry in entries)
            {
                if (!MatchesFilters(entry.Asset))
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(entry.Asset);
                so.Update();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(20));
                bool expanded = GetFoldout(entry.Asset);
                expanded = EditorGUILayout.Foldout(expanded, new GUIContent($"{entry.Asset.DisplayName} ({entry.Asset.Id})"));
                SetFoldout(entry.Asset, expanded);
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(entry.Asset);
                }
                EditorGUILayout.EndHorizontal();

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(so.FindProperty("id"));
                    EditorGUILayout.PropertyField(so.FindProperty("displayName"));
                    SerializedProperty iconProp = so.FindProperty("icon");
                    EditorGUILayout.PropertyField(iconProp);

                    SerializedProperty tagsProp = so.FindProperty("tags");
                    if (tagsProp != null)
                    {
                        EditorGUILayout.PropertyField(tagsProp, true);
                    }

                    drawFields?.Invoke(so);

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(entry.Asset);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAbilityFields(SerializedObject so)
        {
            SerializedProperty descriptionProp = so.FindProperty("description");
            SerializedProperty cooldownProp = so.FindProperty("cooldownSeconds");
            SerializedProperty costsProp = so.FindProperty("resourceCosts");
            SerializedProperty baseEffectProp = so.FindProperty("baseEffectValue");
            SerializedProperty curveProp = so.FindProperty("effectGrowthCurve");
            SerializedProperty useFormulaProp = so.FindProperty("useEffectFormula");
            SerializedProperty formulaProp = so.FindProperty("effectFormula");
            SerializedProperty summaryProp = so.FindProperty("effectSummary");

            EditorGUILayout.PropertyField(descriptionProp);
            EditorGUILayout.PropertyField(cooldownProp);
            EditorGUILayout.PropertyField(costsProp, true);
            EditorGUILayout.PropertyField(baseEffectProp);
            EditorGUILayout.PropertyField(useFormulaProp);
            if (useFormulaProp.boolValue)
            {
                EditorGUILayout.PropertyField(formulaProp);
            }
            else
            {
                EditorGUILayout.PropertyField(curveProp);
            }

            EditorGUILayout.PropertyField(summaryProp);
        }

        private void DrawClassFields(SerializedObject so)
        {
            SerializedProperty descriptionProp = so.FindProperty("description");
            SerializedProperty baseStatsProp = so.FindProperty("baseStats");
            SerializedProperty abilitiesProp = so.FindProperty("abilities");

            EditorGUILayout.PropertyField(descriptionProp);
            EditorGUILayout.PropertyField(baseStatsProp);
            EditorGUILayout.PropertyField(abilitiesProp, true);
        }

        private void DuplicateSelected()
        {
            IEnumerable<IIdentifiableDefinition> selected = abilityEntries.Where(e => e.Selected).Select(e => (IIdentifiableDefinition)e.Asset)
                .Concat(classEntries.Where(e => e.Selected).Select(e => (IIdentifiableDefinition)e.Asset));

            foreach (IIdentifiableDefinition definition in selected)
            {
                ScriptableObject original = (ScriptableObject)definition;
                string path = AssetDatabase.GetAssetPath(original);
                string newId = GenerateUniqueId(definition.Id + duplicateSuffix, original.GetType());
                string newName = string.IsNullOrWhiteSpace(definition.DisplayName) ? newId : definition.DisplayName + duplicateSuffix;

                ScriptableObject copy = Instantiate(original);
                if (copy is IIdentifiableDefinition identifiable)
                {
                    identifiable.SetIdentity(newId, newName);
                }

                string newPath = AssetDatabase.GenerateUniqueAssetPath(path.Replace(original.name, newName));
                AssetDatabase.CreateAsset(copy, newPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(newPath);
            }

            RefreshData();
        }

        private void AssignDefaultIcons()
        {
            if (defaultIcon == null)
            {
                EditorUtility.DisplayDialog("Default Icon Missing", "Select a default icon before assigning.", "OK");
                return;
            }

            foreach (DefinitionEntry<AbilityDefinition> entry in abilityEntries.Where(e => e.Selected))
            {
                if (entry.Asset.Icon == null)
                {
                    Undo.RecordObject(entry.Asset, "Assign Default Icon");
                    SerializedObject so = new SerializedObject(entry.Asset);
                    so.FindProperty("icon").objectReferenceValue = defaultIcon;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(entry.Asset);
                }
            }

            foreach (DefinitionEntry<ClassDefinition> entry in classEntries.Where(e => e.Selected))
            {
                if (entry.Asset.Icon == null)
                {
                    Undo.RecordObject(entry.Asset, "Assign Default Icon");
                    SerializedObject so = new SerializedObject(entry.Asset);
                    so.FindProperty("icon").objectReferenceValue = defaultIcon;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(entry.Asset);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private void ApplyStatPreset()
        {
            if (statPreset == null)
            {
                EditorUtility.DisplayDialog("Stat Preset Missing", "Select a stat preset before applying.", "OK");
                return;
            }

            foreach (DefinitionEntry<ClassDefinition> entry in classEntries.Where(e => e.Selected))
            {
                Undo.RecordObject(entry.Asset, "Apply Stat Preset");
                SerializedObject so = new SerializedObject(entry.Asset);
                so.FindProperty("baseStats").objectReferenceValue = statPreset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(entry.Asset);
            }

            AssetDatabase.SaveAssets();
        }

        private void FixBrokenReferences()
        {
            foreach (DefinitionEntry<ClassDefinition> entry in classEntries.Where(e => e.Selected))
            {
                SerializedObject so = new SerializedObject(entry.Asset);
                SerializedProperty abilitiesProp = so.FindProperty("abilities");
                bool removed = false;
                for (int i = abilitiesProp.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = abilitiesProp.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == null)
                    {
                        abilitiesProp.DeleteArrayElementAtIndex(i);
                        removed = true;
                    }
                }

                if (removed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(entry.Asset);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private void DrawValidationPanel()
        {
            List<ValidationIssue> issues = GatherValidationIssues();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues detected.", MessageType.Info);
            }
            else
            {
                foreach (ValidationIssue issue in issues)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                    Rect labelRect = GUILayoutUtility.GetLastRect();
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }

                    EditorGUILayout.EndHorizontal();

                    if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition) && Event.current.clickCount == 2)
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private List<ValidationIssue> GatherValidationIssues()
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            IEnumerable<IDEntry> idEntries = abilityEntries.Select(e => new IDEntry(e.Asset.Id, e.Asset))
                .Concat(classEntries.Select(e => new IDEntry(e.Asset.Id, e.Asset)));
            ValidateDuplicates(issues, idEntries);

            foreach (DefinitionEntry<AbilityDefinition> entry in abilityEntries)
            {
                if (entry.Asset.Icon == null)
                {
                    issues.Add(new ValidationIssue($"Ability '{entry.Asset.DisplayName}' is missing an icon.", entry.Asset));
                }

                if (!entry.Asset.UseEffectFormula && (entry.Asset.EffectGrowthCurve == null || entry.Asset.EffectGrowthCurve.length == 0))
                {
                    issues.Add(new ValidationIssue($"Ability '{entry.Asset.DisplayName}' has an invalid effect curve.", entry.Asset));
                }
            }

            foreach (DefinitionEntry<ClassDefinition> entry in classEntries)
            {
                if (entry.Asset.Icon == null)
                {
                    issues.Add(new ValidationIssue($"Class '{entry.Asset.DisplayName}' is missing an icon.", entry.Asset));
                }

                if (entry.Asset.Abilities.Any(a => a == null))
                {
                    issues.Add(new ValidationIssue($"Class '{entry.Asset.DisplayName}' has missing ability references.", entry.Asset));
                }
            }

            string[] statGuids = AssetDatabase.FindAssets("t:StatBlock");
            foreach (string guid in statGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatBlock statBlock = AssetDatabase.LoadAssetAtPath<StatBlock>(path);
                if (statBlock == null)
                {
                    continue;
                }

                if (statBlock.Icon == null)
                {
                    issues.Add(new ValidationIssue($"Stat Block '{statBlock.DisplayName}' is missing an icon.", statBlock));
                }

                foreach (StatGrowth growth in statBlock.Stats)
                {
                    if (!growth.UseFormula && (growth.GrowthCurve == null || growth.GrowthCurve.length == 0))
                    {
                        issues.Add(new ValidationIssue($"Stat Block '{statBlock.DisplayName}' has an invalid curve for stat '{growth.StatId}'.", statBlock));
                    }
                }
            }

            return issues;
        }

        private void ValidateDuplicates(List<ValidationIssue> issues, IEnumerable<IDEntry> entries)
        {
            Dictionary<string, List<UnityEngine.Object>> seen = new Dictionary<string, List<UnityEngine.Object>>();
            foreach (IDEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                if (!seen.TryGetValue(entry.Id, out List<UnityEngine.Object> list))
                {
                    list = new List<UnityEngine.Object>();
                    seen[entry.Id] = list;
                }

                list.Add(entry.Context);
            }

            foreach (KeyValuePair<string, List<UnityEngine.Object>> pair in seen.Where(p => p.Value.Count > 1))
            {
                foreach (UnityEngine.Object context in pair.Value)
                {
                    issues.Add(new ValidationIssue($"Duplicate ID '{pair.Key}' detected.", context));
                }
            }
        }

        private bool MatchesFilters(IIdentifiableDefinition definition)
        {
            bool matchesSearch = string.IsNullOrWhiteSpace(searchTerm)
                || (!string.IsNullOrWhiteSpace(definition.DisplayName) && definition.DisplayName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(definition.Id) && definition.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!matchesSearch)
            {
                return false;
            }

            if (tagFilterIndex == 0)
            {
                return true;
            }

            string selectedTag = tagOptions[tagFilterIndex];
            if (definition is AbilityDefinition ability)
            {
                return ability.Tags.Contains(selectedTag);
            }

            if (definition is ClassDefinition classDef)
            {
                return classDef.Tags.Contains(selectedTag);
            }

            return true;
        }

        private void AddTags(IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                if (!tagOptions.Contains(tag))
                {
                    tagOptions.Add(tag);
                }
            }
        }

        private bool GetFoldout(UnityEngine.Object key)
        {
            if (!foldoutStates.TryGetValue(key, out bool expanded))
            {
                expanded = false;
                foldoutStates[key] = expanded;
            }

            return expanded;
        }

        private void SetFoldout(UnityEngine.Object key, bool state)
        {
            foldoutStates[key] = state;
        }

        private string GenerateUniqueId(string baseId, Type type)
        {
            string candidate = baseId;
            int counter = 1;
            while (!IsIdUniqueForType(candidate, type))
            {
                candidate = baseId + counter;
                counter++;
            }

            return candidate;
        }

        private bool IsIdUniqueForType(string candidate, Type type)
        {
            string filter = $"t:{type.Name}";
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset is IIdentifiableDefinition identifiable && identifiable.Id == candidate)
                {
                    return false;
                }
            }

            return true;
        }

        private struct DefinitionEntry<T> where T : ScriptableObject, IIdentifiableDefinition
        {
            public DefinitionEntry(T asset)
            {
                Asset = asset;
                Selected = false;
            }

            public T Asset { get; }
            public bool Selected { get; set; }
        }

        private struct ValidationIssue
        {
            public ValidationIssue(string message, UnityEngine.Object context)
            {
                Message = message;
                Context = context;
            }

            public string Message { get; }
            public UnityEngine.Object Context { get; }
        }

        private struct IDEntry
        {
            public IDEntry(string id, UnityEngine.Object context)
            {
                Id = id;
                Context = context;
            }

            public string Id { get; }
            public UnityEngine.Object Context { get; }
        }
    }
}
