using System.IO;
using Adventure.UI.ClassSelection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Adventure.Editor.UI
{
    public static class ClassSelectionPrefabUtility
    {
        private const string RootFolder = "Assets/UI/Prefabs/ClassSelection";
        private const string TagBadgePath = RootFolder + "/ClassTagBadge.prefab";
        private const string StatEntryPath = RootFolder + "/ClassStatEntry.prefab";
        private const string AbilityEntryPath = RootFolder + "/ClassAbilityEntry.prefab";
        private const string ButtonPath = RootFolder + "/ClassSelectButton.prefab";
        private const string SelectionPanelPath = RootFolder + "/ClassSelectionPanel.prefab";

        private struct PrefabCache
        {
            public GameObject TagBadgePrefab;
            public GameObject StatEntryPrefab;
            public GameObject AbilityEntryPrefab;
            public GameObject ButtonPrefab;
            public GameObject SelectionPanelPrefab;
        }

        [InitializeOnLoadMethod]
        private static void EnsurePrefabsOnLoad()
        {
            EnsurePrefabs();
        }

        [MenuItem("GameObject/Adventure UI/Class Selection Panel", false, 10)]
        public static void CreatePanel(MenuCommand command)
        {
            PrefabCache prefabs = EnsurePrefabs();

            GameObject canvas = GetOrCreateCanvas();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabs.SelectionPanelPrefab, canvas.transform) as GameObject;
            if (instance != null)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Create Class Selection Panel");
                Selection.activeGameObject = instance;
            }
        }

        private static PrefabCache EnsurePrefabs()
        {
            if (!Directory.Exists(RootFolder))
            {
                Directory.CreateDirectory(RootFolder);
                AssetDatabase.Refresh();
            }

            GameObject tagBadge = LoadOrCreate(TagBadgePath, CreateTagBadgePrefab);
            GameObject statEntry = LoadOrCreate(StatEntryPath, CreateStatEntryPrefab);
            GameObject abilityEntry = LoadOrCreate(AbilityEntryPath, CreateAbilityEntryPrefab);
            GameObject button = LoadOrCreate(ButtonPath, () => CreateClassButtonPrefab(tagBadge));
            GameObject selectionPanel = LoadOrCreate(SelectionPanelPath, () => CreateSelectionPanelPrefab(button, statEntry, abilityEntry));

            return new PrefabCache
            {
                TagBadgePrefab = tagBadge,
                StatEntryPrefab = statEntry,
                AbilityEntryPrefab = abilityEntry,
                ButtonPrefab = button,
                SelectionPanelPrefab = selectionPanel
            };
        }

        private static GameObject LoadOrCreate(string path, System.Func<GameObject> createFunc)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            GameObject created = createFunc();
            if (created == null)
            {
                return null;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(created, path);
            Object.DestroyImmediate(created);
            return prefab;
        }

        private static GameObject CreateTagBadgePrefab()
        {
            GameObject badge = CreateUIObject("ClassTagBadge");
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100f, 30f);

            Image background = badge.AddComponent<Image>();
            background.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            HorizontalLayoutGroup layout = badge.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childAlignment = TextAnchor.MiddleCenter;

            TMP_Text label = CreateTMPText("Label", badge.transform);
            label.text = "Tag";
            label.alignment = TextAlignmentOptions.Midline;
            label.fontSize = 18f;

            LayoutElement element = badge.AddComponent<LayoutElement>();
            element.minHeight = 30f;

            return badge;
        }

        private static GameObject CreateStatEntryPrefab()
        {
            GameObject entry = CreateUIObject("ClassStatEntry");
            HorizontalLayoutGroup layout = entry.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;

            TMP_Text statLabel = CreateTMPText("StatName", entry.transform);
            statLabel.text = "Stat";
            statLabel.fontSize = 20f;

            TMP_Text valueLabel = CreateTMPText("StatValue", entry.transform);
            valueLabel.text = "0";
            valueLabel.fontSize = 20f;

            LayoutElement element = entry.AddComponent<LayoutElement>();
            element.minHeight = 28f;

            return entry;
        }

        private static GameObject CreateAbilityEntryPrefab()
        {
            GameObject entry = CreateUIObject("ClassAbilityEntry");
            VerticalLayoutGroup layout = entry.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;

            TMP_Text nameLabel = CreateTMPText("AbilityName", entry.transform);
            nameLabel.text = "Ability";
            nameLabel.fontSize = 22f;
            nameLabel.fontStyle = FontStyles.Bold;

            TMP_Text summaryLabel = CreateTMPText("AbilitySummary", entry.transform);
            summaryLabel.text = "Summary";
            summaryLabel.fontSize = 18f;
            summaryLabel.enableWordWrapping = true;

            LayoutElement element = entry.AddComponent<LayoutElement>();
            element.minHeight = 40f;

            return entry;
        }

        private static GameObject CreateClassButtonPrefab(GameObject tagBadgePrefab)
        {
            GameObject root = CreateUIObject("ClassSelectButton");
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 80f);

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            Button button = root.AddComponent<Button>();

            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            Image icon = CreateUIObject("Icon", root.transform).AddComponent<Image>();
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(64f, 64f);
            icon.preserveAspect = true;

            VerticalLayoutGroup textColumn = CreateUIObject("TextColumn", root.transform).AddComponent<VerticalLayoutGroup>();
            textColumn.childAlignment = TextAnchor.MiddleLeft;
            textColumn.spacing = 4f;
            textColumn.childForceExpandWidth = false;
            textColumn.childForceExpandHeight = false;

            TMP_Text nameLabel = CreateTMPText("Name", textColumn.transform);
            nameLabel.text = "Class Name";
            nameLabel.fontSize = 24f;
            nameLabel.fontStyle = FontStyles.Bold;

            RectTransform badgeContainer = CreateUIObject("Badges", textColumn.transform).GetComponent<RectTransform>();
            HorizontalLayoutGroup badgeLayout = badgeContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeLayout.spacing = 6f;
            badgeLayout.childForceExpandWidth = false;
            badgeLayout.childForceExpandHeight = false;

            LayoutElement layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.minHeight = 80f;

            ClassSelectButton binding = root.AddComponent<ClassSelectButton>();
            SerializedObject bindingSO = new SerializedObject(binding);
            bindingSO.FindProperty("iconImage").objectReferenceValue = icon;
            bindingSO.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            bindingSO.FindProperty("badgeContainer").objectReferenceValue = badgeContainer;
            bindingSO.FindProperty("badgePrefab").objectReferenceValue = tagBadgePrefab;
            bindingSO.FindProperty("button").objectReferenceValue = button;
            bindingSO.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateSelectionPanelPrefab(GameObject buttonPrefab, GameObject statEntryPrefab, GameObject abilityEntryPrefab)
        {
            GameObject root = CreateUIObject("ClassSelectionPanel");
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900f, 500f);

            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;

            // List area
            GameObject listWrapper = CreateUIObject("ClassList", root.transform);
            VerticalLayoutGroup listLayout = listWrapper.AddComponent<VerticalLayoutGroup>();
            listLayout.childForceExpandHeight = true;
            listLayout.childForceExpandWidth = true;

            GameObject scrollView = CreateUIObject("Scroll View", listWrapper.transform);
            RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.sizeDelta = new Vector2(300f, 0f);
            Image scrollBackground = scrollView.AddComponent<Image>();
            scrollBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            GameObject viewport = CreateUIObject("Viewport", scrollView.transform);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            GameObject content = CreateUIObject("Content", viewport.transform);
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childControlWidth = true;
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content.GetComponent<RectTransform>();

            // Details area
            GameObject details = CreateUIObject("Details", root.transform);
            VerticalLayoutGroup detailsLayout = details.AddComponent<VerticalLayoutGroup>();
            detailsLayout.spacing = 8f;
            detailsLayout.padding = new RectOffset(10, 10, 10, 10);
            detailsLayout.childControlHeight = false;
            detailsLayout.childForceExpandHeight = false;

            GameObject header = CreateUIObject("Header", details.transform);
            HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            Image icon = CreateUIObject("Icon", header.transform).AddComponent<Image>();
            icon.rectTransform.sizeDelta = new Vector2(96f, 96f);
            icon.preserveAspect = true;

            TMP_Text nameLabel = CreateTMPText("Name", header.transform);
            nameLabel.fontSize = 30f;
            nameLabel.fontStyle = FontStyles.Bold;

            TMP_Text description = CreateTMPText("Description", details.transform);
            description.enableWordWrapping = true;
            description.fontSize = 18f;

            TMP_Text statHeader = CreateTMPText("StatsHeader", details.transform);
            statHeader.text = "Stats";
            statHeader.fontStyle = FontStyles.Bold;
            statHeader.fontSize = 20f;

            RectTransform statsContainer = CreateUIObject("Stats", details.transform).GetComponent<RectTransform>();
            VerticalLayoutGroup statsLayout = statsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 4f;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandHeight = false;

            TMP_Text abilityHeader = CreateTMPText("AbilitiesHeader", details.transform);
            abilityHeader.text = "Abilities";
            abilityHeader.fontStyle = FontStyles.Bold;
            abilityHeader.fontSize = 20f;

            RectTransform abilityContainer = CreateUIObject("Abilities", details.transform).GetComponent<RectTransform>();
            VerticalLayoutGroup abilityLayout = abilityContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            abilityLayout.spacing = 6f;
            abilityLayout.childControlHeight = true;
            abilityLayout.childForceExpandHeight = false;

            GameObject selectButtonObj = CreateUIObject("SelectButton", details.transform);
            Image selectBg = selectButtonObj.AddComponent<Image>();
            selectBg.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            Button selectButton = selectButtonObj.AddComponent<Button>();
            TMP_Text selectLabel = CreateTMPText("Label", selectButtonObj.transform);
            selectLabel.alignment = TextAlignmentOptions.Center;
            selectLabel.fontSize = 22f;
            selectLabel.text = "Select";
            HorizontalLayoutGroup selectLayout = selectButtonObj.AddComponent<HorizontalLayoutGroup>();
            selectLayout.childAlignment = TextAnchor.MiddleCenter;

            LayoutElement detailsElement = details.AddComponent<LayoutElement>();
            detailsElement.minWidth = 400f;

            ClassSelectionController controller = root.AddComponent<ClassSelectionController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("listContainer").objectReferenceValue = content.GetComponent<RectTransform>();
            serializedController.FindProperty("buttonPrefab").objectReferenceValue = buttonPrefab;
            serializedController.FindProperty("detailIcon").objectReferenceValue = icon;
            serializedController.FindProperty("detailName").objectReferenceValue = nameLabel;
            serializedController.FindProperty("detailDescription").objectReferenceValue = description;
            serializedController.FindProperty("statContainer").objectReferenceValue = statsContainer;
            serializedController.FindProperty("statEntryPrefab").objectReferenceValue = statEntryPrefab;
            serializedController.FindProperty("abilityContainer").objectReferenceValue = abilityContainer;
            serializedController.FindProperty("abilityEntryPrefab").objectReferenceValue = abilityEntryPrefab;
            serializedController.FindProperty("selectButton").objectReferenceValue = selectButton;
            serializedController.FindProperty("selectButtonLabel").objectReferenceValue = selectLabel;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateUIObject(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        private static TMP_Text CreateTMPText(string name, Transform parent)
        {
            GameObject go = CreateUIObject(name, parent);
            go.AddComponent<CanvasRenderer>();
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = name;
            text.fontSize = 18f;
            text.color = Color.white;
            text.enableWordWrapping = false;
            return text;
        }

        private static GameObject GetOrCreateCanvas()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                return canvas.gameObject;
            }

            GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            EnsureEventSystem();

            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return go;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }
    }
}
