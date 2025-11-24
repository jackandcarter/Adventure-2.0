using System;
using Adventure.GameData.Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Adventure.UI.ClassSelection
{
    [RequireComponent(typeof(Button))]
    public class ClassSelectButton : MonoBehaviour
    {
        [Serializable]
        public class ClassClickedEvent : UnityEvent<ClassDefinition>
        {
        }

        [SerializeField]
        private ClassDefinition classDefinition;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TMP_Text nameLabel;

        [SerializeField]
        private RectTransform badgeContainer;

        [SerializeField]
        private GameObject badgePrefab;

        [SerializeField]
        private Button button;

        [SerializeField]
        private ClassClickedEvent onClicked = new ClassClickedEvent();

        public ClassDefinition ClassDefinition => classDefinition;

        public ClassClickedEvent OnClicked => onClicked;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void SetClass(ClassDefinition definition)
        {
            classDefinition = definition;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (iconImage != null)
            {
                iconImage.sprite = classDefinition != null ? classDefinition.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameLabel != null)
            {
                nameLabel.text = classDefinition != null ? classDefinition.DisplayName : string.Empty;
            }

            if (badgeContainer != null)
            {
                ClearChildren(badgeContainer);

                if (classDefinition != null && badgePrefab != null)
                {
                    foreach (string tag in classDefinition.Tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag))
                        {
                            continue;
                        }

                        GameObject badge = Instantiate(badgePrefab, badgeContainer);
                        TMP_Text label = badge.GetComponentInChildren<TMP_Text>();
                        if (label != null)
                        {
                            label.text = tag;
                        }
                    }
                }
            }
        }

        private static void ClearChildren(Transform target)
        {
            if (target == null)
            {
                return;
            }

            for (int i = target.childCount - 1; i >= 0; i--)
            {
                Transform child = target.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Destroy(child.gameObject);
            }
        }

        private void HandleClick()
        {
            if (classDefinition != null)
            {
                onClicked?.Invoke(classDefinition);
            }
        }
    }
}
