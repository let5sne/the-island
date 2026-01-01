using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Models;
using TheIsland.Network;

namespace TheIsland.UI
{
    /// <summary>
    /// Creates and manages floating UI above an agent (name, HP/Energy bars, speech bubble).
    /// Attach this to the Agent prefab - it will create all UI elements automatically.
    /// </summary>
    public class AgentUI : MonoBehaviour
    {
        #region Configuration
        [Header("UI Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2.5f, 0);
        [SerializeField] private float uiScale = 0.01f;
        [SerializeField] private float speechDuration = 5f;

        [Header("Colors")]
        [SerializeField] private Color hpHighColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color hpLowColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color energyHighColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color energyLowColor = new Color(1f, 0.5f, 0.1f);
        #endregion

        #region UI References
        private Canvas _canvas;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _personalityLabel;
        private Image _hpBarFill;
        private Image _energyBarFill;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _energyText;
        private GameObject _speechBubble;
        private TextMeshProUGUI _speechText;
        private GameObject _deathOverlay;
        #endregion

        #region State
        private int _agentId;
        private AgentData _currentData;
        private Coroutine _speechCoroutine;
        private Camera _mainCamera;
        #endregion

        #region Properties
        public int AgentId => _agentId;
        public AgentData CurrentData => _currentData;
        public bool IsAlive => _currentData?.IsAlive ?? false;
        #endregion

        #region Initialization
        private void Awake()
        {
            _mainCamera = Camera.main;
            CreateUI();
        }

        private void LateUpdate()
        {
            // Make UI face the camera (billboard effect)
            if (_canvas != null && _mainCamera != null)
            {
                _canvas.transform.LookAt(
                    _canvas.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up
                );
            }
        }

        public void Initialize(AgentData data)
        {
            _agentId = data.id;
            _currentData = data;
            gameObject.name = $"Agent_{data.id}_{data.name}";

            // Set name and personality
            if (_nameLabel != null) _nameLabel.text = data.name;
            if (_personalityLabel != null) _personalityLabel.text = $"({data.personality})";

            UpdateStats(data);
            Debug.Log($"[AgentUI] Initialized {data.name}");
        }
        #endregion

        #region UI Creation
        private void CreateUI()
        {
            // Create World Space Canvas
            var canvasObj = new GameObject("AgentCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = uiOffset;
            canvasObj.transform.localScale = Vector3.one * uiScale;

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300, 200);

            // Create UI Panel
            var panel = CreatePanel(canvasObj.transform, "UIPanel", new Vector2(300, 150));

            // Name Label
            _nameLabel = CreateText(panel, "NameLabel", "Agent", 32, Color.white, FontStyles.Bold);
            SetRectPosition(_nameLabel.rectTransform, 0, 60, 280, 40);

            // Personality Label
            _personalityLabel = CreateText(panel, "PersonalityLabel", "(personality)", 18,
                new Color(0.7f, 0.7f, 0.7f), FontStyles.Italic);
            SetRectPosition(_personalityLabel.rectTransform, 0, 30, 280, 25);

            // HP Bar
            var hpBar = CreateBar(panel, "HPBar", "HP", hpHighColor, out _hpBarFill, out _hpText);
            SetRectPosition(hpBar, 0, -5, 250, 22);

            // Energy Bar
            var energyBar = CreateBar(panel, "EnergyBar", "Energy", energyHighColor, out _energyBarFill, out _energyText);
            SetRectPosition(energyBar, 0, -35, 250, 22);

            // Death Overlay
            _deathOverlay = CreateDeathOverlay(panel);

            // Speech Bubble (positioned above the main UI)
            _speechBubble = CreateSpeechBubble(canvasObj.transform);
            _speechBubble.SetActive(false);

            // Add collider for click detection if not present
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.5f;
                col.center = new Vector3(0, 1, 0);
            }
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = Vector3.one;

            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            // Semi-transparent glass background
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.18f, 0.6f);

            // Add border highlight
            var border = new GameObject("Border");
            border.transform.SetParent(panel.transform);
            var bRect = border.AddComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-1, -1);
            bRect.offsetMax = new Vector2(1, 1);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(1f, 1f, 1f, 0.1f);
            border.transform.SetAsFirstSibling();

            return panel;
        }

        private float _currentHp;
        private float _currentEnergy;

        private void Update()
        {
            if (_hpBarFill != null && _currentData != null)
            {
                _currentHp = Mathf.Lerp(_currentHp, _currentData.hp, Time.deltaTime * 5f);
                float hpPercent = _currentHp / 100f;
                _hpBarFill.rectTransform.anchorMax = new Vector2(hpPercent, 1);
                _hpBarFill.color = Color.Lerp(hpLowColor, hpHighColor, hpPercent);
            }

            if (_energyBarFill != null && _currentData != null)
            {
                _currentEnergy = Mathf.Lerp(_currentEnergy, _currentData.energy, Time.deltaTime * 5f);
                float energyPercent = _currentEnergy / 100f;
                _energyBarFill.rectTransform.anchorMax = new Vector2(energyPercent, 1);
                _energyBarFill.color = Color.Lerp(energyLowColor, energyHighColor, energyPercent);
            }
        }

        private TextMeshProUGUI CreateText(GameObject parent, string name, string text,
            float fontSize, Color color, FontStyles style = FontStyles.Normal)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;

            return tmp;
        }

        private RectTransform CreateBar(GameObject parent, string name, string label,
            Color fillColor, out Image fillImage, out TextMeshProUGUI valueText)
        {
            var barContainer = new GameObject(name);
            barContainer.transform.SetParent(parent.transform);
            barContainer.transform.localPosition = Vector3.zero;
            barContainer.transform.localRotation = Quaternion.identity;
            barContainer.transform.localScale = Vector3.one;

            var containerRect = barContainer.AddComponent<RectTransform>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(barContainer.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(barContainer.transform);
            fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);

            // Label + Value Text
            valueText = CreateText(barContainer, "Text", $"{label}: 100", 14, Color.white);
            var textRect = valueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return containerRect;
        }

        private GameObject CreateDeathOverlay(GameObject parent)
        {
            var overlay = new GameObject("DeathOverlay");
            overlay.transform.SetParent(parent.transform);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;

            var rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = overlay.AddComponent<Image>();
            img.color = new Color(0.3f, 0f, 0f, 0.7f);

            var deathText = CreateText(overlay, "DeathText", "DEAD", 28, Color.red, FontStyles.Bold);
            deathText.rectTransform.anchorMin = Vector2.zero;
            deathText.rectTransform.anchorMax = Vector2.one;
            deathText.rectTransform.offsetMin = Vector2.zero;
            deathText.rectTransform.offsetMax = Vector2.zero;

            overlay.SetActive(false);
            return overlay;
        }

        private GameObject CreateSpeechBubble(Transform parent)
        {
            var bubble = new GameObject("SpeechBubble");
            bubble.transform.SetParent(parent);
            bubble.transform.localPosition = new Vector3(0, 100, 0); // Above main UI
            bubble.transform.localRotation = Quaternion.identity;
            bubble.transform.localScale = Vector3.one;

            var rect = bubble.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350, 80);

            // Bubble background
            var bg = bubble.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.95f);

            // Speech text
            _speechText = CreateText(bubble, "SpeechText", "", 20, new Color(0.2f, 0.2f, 0.2f));
            _speechText.alignment = TextAlignmentOptions.Center;
            _speechText.textWrappingMode = TextWrappingModes.Normal;
            var textRect = _speechText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 10);
            textRect.offsetMax = new Vector2(-15, -10);

            // Bubble tail (triangle pointing down)
            var tail = new GameObject("Tail");
            tail.transform.SetParent(bubble.transform);
            var tailRect = tail.AddComponent<RectTransform>();
            tailRect.anchoredPosition = new Vector2(0, -35);
            tailRect.sizeDelta = new Vector2(20, 15);

            return bubble;
        }

        private void SetRectPosition(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
        #endregion

        #region Stats Update
        public void UpdateStats(AgentData data)
        {
            _currentData = data;

            if (_hpText != null)
            {
                _hpText.text = $"HP: {data.hp}";
            }

            if (_energyText != null)
            {
                _energyText.text = $"Energy: {data.energy}";
            }

            // Update death state
            if (!data.IsAlive)
            {
                OnDeath();
            }
            else
            {
                OnAlive();
            }
        }

        private void OnDeath()
        {
            if (_deathOverlay != null) _deathOverlay.SetActive(true);
            HideSpeech();

            // Gray out the character
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material != null)
                {
                    r.material.color = new Color(0.3f, 0.3f, 0.3f);
                }
            }
        }

        private void OnAlive()
        {
            if (_deathOverlay != null) _deathOverlay.SetActive(false);

            // Restore color
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material != null)
                {
                    r.material.color = Color.white;
                }
            }
        }
        #endregion

        #region Speech Bubble
        public void ShowSpeech(string text)
        {
            if (_speechBubble == null || !IsAlive) return;

            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
            }

            _speechText.text = text;
            _speechBubble.SetActive(true);

            _speechCoroutine = StartCoroutine(HideSpeechAfterDelay());
            Debug.Log($"[AgentUI] {_currentData?.name} says: \"{text}\"");
        }

        private IEnumerator HideSpeechAfterDelay()
        {
            yield return new WaitForSeconds(speechDuration);
            HideSpeech();
        }

        public void HideSpeech()
        {
            if (_speechBubble != null)
            {
                _speechBubble.SetActive(false);
            }
        }
        #endregion

        #region Interaction
        private void OnMouseDown()
        {
            if (!IsAlive)
            {
                Debug.Log($"[AgentUI] Cannot feed dead agent: {_currentData?.name}");
                return;
            }

            NetworkManager.Instance.FeedAgent(_currentData.name);
            Debug.Log($"[AgentUI] Clicked to feed: {_currentData?.name}");
        }
        #endregion
    }
}
