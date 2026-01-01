using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Models;
using TheIsland.Network;

namespace TheIsland.Visual
{
    /// <summary>
    /// Complete 2.5D Agent visual system.
    /// Creates sprite, floating UI, and speech bubble programmatically.
    /// Attach to an empty GameObject to create a full agent visual.
    /// </summary>
    public class AgentVisual : MonoBehaviour
    {
        #region Configuration
        [Header("Sprite Settings")]
        [Tooltip("Assign a sprite, or leave empty for auto-generated placeholder")]
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private Color spriteColor = Color.white;
        [SerializeField] private float spriteScale = 2f;
        [SerializeField] private int sortingOrder = 10;

        [Header("Placeholder Colors (if no sprite assigned)")]
        [SerializeField] private Color placeholderBodyColor = new Color(0.3f, 0.6f, 0.9f);
        [SerializeField] private Color placeholderOutlineColor = new Color(0.2f, 0.4f, 0.7f);

        [Header("UI Settings")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 2.2f, 0);
        [SerializeField] private float uiScale = 0.008f;

        [Header("Speech Bubble")]
        [SerializeField] private float speechDuration = 5f;
        [SerializeField] private Vector3 speechOffset = new Vector3(0, 3.5f, 0);

        [Header("Colors")]
        [SerializeField] private Color hpHighColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color hpLowColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color energyHighColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color energyLowColor = new Color(1f, 0.5f, 0.1f);
        #endregion

        #region References
        private SpriteRenderer _spriteRenderer;
        private Canvas _uiCanvas;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _personalityLabel;
        private Image _hpBarFill;
        private Image _energyBarFill;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _energyText;
        private GameObject _deathOverlay;
        private SpeechBubble _speechBubble;
        private Billboard _spriteBillboard;
        private Billboard _uiBillboard;
        private Camera _mainCamera;
        #endregion

        #region State
        private int _agentId;
        private AgentData _currentData;
        private Coroutine _speechCoroutine;
        #endregion

        #region Properties
        public int AgentId => _agentId;
        public AgentData CurrentData => _currentData;
        public bool IsAlive => _currentData?.IsAlive ?? false;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _mainCamera = Camera.main;
            CreateVisuals();
        }

        private void OnMouseDown()
        {
            if (!IsAlive)
            {
                Debug.Log($"[AgentVisual] Cannot interact with dead agent: {_currentData?.name}");
                return;
            }

            NetworkManager.Instance?.FeedAgent(_currentData.name);
            Debug.Log($"[AgentVisual] Clicked to feed: {_currentData?.name}");
        }
        #endregion

        #region Initialization
        public void Initialize(AgentData data)
        {
            _agentId = data.id;
            _currentData = data;
            gameObject.name = $"Agent_{data.id}_{data.name}";

            // Apply unique color based on agent ID
            ApplyAgentColor(data.id);

            // Set UI text
            if (_nameLabel != null) _nameLabel.text = data.name;
            if (_personalityLabel != null) _personalityLabel.text = $"({data.personality})";

            UpdateStats(data);
            Debug.Log($"[AgentVisual] Initialized: {data.name}");
        }

        private void ApplyAgentColor(int agentId)
        {
            // Generate unique color per agent
            Color[] agentColors = new Color[]
            {
                new Color(0.3f, 0.6f, 0.9f),  // Blue (Jack)
                new Color(0.9f, 0.5f, 0.7f),  // Pink (Luna)
                new Color(0.5f, 0.8f, 0.5f),  // Green (Bob)
                new Color(0.9f, 0.7f, 0.3f),  // Orange
                new Color(0.7f, 0.5f, 0.9f),  // Purple
            };

            int colorIndex = agentId % agentColors.Length;
            placeholderBodyColor = agentColors[colorIndex];
            placeholderOutlineColor = agentColors[colorIndex] * 0.7f;

            // Update sprite color if using placeholder
            if (_spriteRenderer != null && characterSprite == null)
            {
                RegeneratePlaceholderSprite();
            }
        }
        #endregion

        #region Visual Creation
        private void CreateVisuals()
        {
            CreateSprite();
            CreateUICanvas();
            CreateSpeechBubble();
            CreateCollider();
        }

        private void CreateSprite()
        {
            // Create sprite child object
            var spriteObj = new GameObject("CharacterSprite");
            spriteObj.transform.SetParent(transform);
            spriteObj.transform.localPosition = new Vector3(0, 1f, 0);
            spriteObj.transform.localScale = Vector3.one * spriteScale;

            _spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
            _spriteRenderer.sortingOrder = sortingOrder;

            if (characterSprite != null)
            {
                _spriteRenderer.sprite = characterSprite;
                _spriteRenderer.color = spriteColor;
            }
            else
            {
                // Generate placeholder sprite
                RegeneratePlaceholderSprite();
            }

            // Add billboard
            _spriteBillboard = spriteObj.AddComponent<Billboard>();
        }

        private void RegeneratePlaceholderSprite()
        {
            if (_spriteRenderer == null) return;

            // Create a simple character placeholder (circle with body shape)
            Texture2D texture = CreatePlaceholderTexture(64, 64);
            _spriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        private Texture2D CreatePlaceholderTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            // Clear to transparent
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            // Draw simple character shape
            Vector2 center = new Vector2(width / 2f, height / 2f);

            // Body (ellipse)
            DrawEllipse(pixels, width, height, center + Vector2.down * 8, 14, 20, placeholderBodyColor);

            // Head (circle)
            DrawCircle(pixels, width, height, center + Vector2.up * 12, 12, placeholderBodyColor);

            // Outline
            DrawCircleOutline(pixels, width, height, center + Vector2.up * 12, 12, placeholderOutlineColor, 2);
            DrawEllipseOutline(pixels, width, height, center + Vector2.down * 8, 14, 20, placeholderOutlineColor, 2);

            // Eyes
            DrawCircle(pixels, width, height, center + new Vector2(-4, 14), 2, Color.white);
            DrawCircle(pixels, width, height, center + new Vector2(4, 14), 2, Color.white);
            DrawCircle(pixels, width, height, center + new Vector2(-4, 14), 1, Color.black);
            DrawCircle(pixels, width, height, center + new Vector2(4, 14), 1, Color.black);

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawCircle(Color[] pixels, int width, int height, Vector2 center, float radius, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void DrawCircleOutline(Color[] pixels, int width, int height, Vector2 center, float radius, Color color, int thickness)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= radius - thickness && dist <= radius + thickness)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void DrawEllipse(Color[] pixels, int width, int height, Vector2 center, float rx, float ry, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - center.x) / rx;
                    float dy = (y - center.y) / ry;
                    if (dx * dx + dy * dy <= 1)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void DrawEllipseOutline(Color[] pixels, int width, int height, Vector2 center, float rx, float ry, Color color, int thickness)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - center.x) / rx;
                    float dy = (y - center.y) / ry;
                    float dist = dx * dx + dy * dy;
                    float outer = 1 + (thickness / Mathf.Min(rx, ry));
                    float inner = 1 - (thickness / Mathf.Min(rx, ry));
                    if (dist >= inner && dist <= outer)
                    {
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void CreateUICanvas()
        {
            // World Space Canvas
            var canvasObj = new GameObject("UICanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = uiOffset;
            canvasObj.transform.localScale = Vector3.one * uiScale;

            _uiCanvas = canvasObj.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.WorldSpace;
            _uiCanvas.sortingOrder = sortingOrder + 1;

            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400, 150);

            // Add billboard to canvas (configured for UI - full facing)
            _uiBillboard = canvasObj.AddComponent<Billboard>();
            _uiBillboard.ConfigureForUI();

            // Create UI panel
            var panel = CreateUIPanel(canvasObj.transform, new Vector2(350, 120));

            // Name label
            _nameLabel = CreateUIText(panel.transform, "NameLabel", "Agent", 36, Color.white, FontStyles.Bold);
            SetRectPosition(_nameLabel.rectTransform, 0, 45, 320, 45);

            // Personality label
            _personalityLabel = CreateUIText(panel.transform, "PersonalityLabel", "(Personality)", 20,
                new Color(0.8f, 0.8f, 0.8f), FontStyles.Italic);
            SetRectPosition(_personalityLabel.rectTransform, 0, 15, 320, 25);

            // HP Bar
            var hpBar = CreateProgressBar(panel.transform, "HPBar", "HP", hpHighColor, out _hpBarFill, out _hpText);
            SetRectPosition(hpBar, 0, -15, 280, 24);

            // Energy Bar
            var energyBar = CreateProgressBar(panel.transform, "EnergyBar", "Energy", energyHighColor, out _energyBarFill, out _energyText);
            SetRectPosition(energyBar, 0, -45, 280, 24);

            // Death overlay
            _deathOverlay = CreateDeathOverlay(panel.transform);
            _deathOverlay.SetActive(false);
        }

        private GameObject CreateUIPanel(Transform parent, Vector2 size)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = Vector3.one;

            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            return panel;
        }

        private TextMeshProUGUI CreateUIText(Transform parent, string name, string text,
            float fontSize, Color color, FontStyles style = FontStyles.Normal)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
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

        private RectTransform CreateProgressBar(Transform parent, string name, string label,
            Color fillColor, out Image fillImage, out TextMeshProUGUI valueText)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;

            var containerRect = container.AddComponent<RectTransform>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(container.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.localPosition = Vector3.zero;
            bgRect.localScale = Vector3.one;

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(container.transform);
            fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            fillRect.localPosition = Vector3.zero;
            fillRect.localScale = Vector3.one;

            // Text
            valueText = CreateUIText(container.transform, "Text", $"{label}: 100", 16, Color.white);
            var textRect = valueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return containerRect;
        }

        private GameObject CreateDeathOverlay(Transform parent)
        {
            var overlay = new GameObject("DeathOverlay");
            overlay.transform.SetParent(parent);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;

            var rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = overlay.AddComponent<Image>();
            img.color = new Color(0.2f, 0f, 0f, 0.8f);

            var deathText = CreateUIText(overlay.transform, "DeathText", "DEAD", 32, Color.red, FontStyles.Bold);
            deathText.rectTransform.anchorMin = Vector2.zero;
            deathText.rectTransform.anchorMax = Vector2.one;
            deathText.rectTransform.offsetMin = Vector2.zero;
            deathText.rectTransform.offsetMax = Vector2.zero;

            return overlay;
        }

        private void CreateSpeechBubble()
        {
            // Create speech bubble canvas
            var bubbleCanvas = new GameObject("SpeechCanvas");
            bubbleCanvas.transform.SetParent(transform);
            bubbleCanvas.transform.localPosition = speechOffset;
            bubbleCanvas.transform.localScale = Vector3.one * uiScale;

            var canvas = bubbleCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder + 2;

            var canvasRect = bubbleCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400, 100);

            // Add billboard (configured for UI - full facing)
            var bubbleBillboard = bubbleCanvas.AddComponent<Billboard>();
            bubbleBillboard.ConfigureForUI();

            // Create speech bubble
            _speechBubble = SpeechBubble.Create(bubbleCanvas.transform, Vector3.zero);
            _speechBubble.DisplayDuration = speechDuration;
        }

        private void CreateCollider()
        {
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 2.5f;
                col.radius = 0.6f;
                col.center = new Vector3(0, 1.25f, 0);
            }
        }

        private void SetRectPosition(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localPosition = new Vector3(x, y, 0);
            rect.localScale = Vector3.one;
        }
        #endregion

        #region Stats Update
        public void UpdateStats(AgentData data)
        {
            _currentData = data;

            // Update HP bar
            float hpPercent = data.hp / 100f;
            if (_hpBarFill != null)
            {
                _hpBarFill.rectTransform.anchorMax = new Vector2(hpPercent, 1);
                _hpBarFill.color = Color.Lerp(hpLowColor, hpHighColor, hpPercent);
            }
            if (_hpText != null)
            {
                _hpText.text = $"HP: {data.hp}";
            }

            // Update Energy bar
            float energyPercent = data.energy / 100f;
            if (_energyBarFill != null)
            {
                _energyBarFill.rectTransform.anchorMax = new Vector2(energyPercent, 1);
                _energyBarFill.color = Color.Lerp(energyLowColor, energyHighColor, energyPercent);
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
            if (_speechBubble != null) _speechBubble.Hide();

            // Gray out sprite
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
            }
        }

        private void OnAlive()
        {
            if (_deathOverlay != null) _deathOverlay.SetActive(false);

            // Restore sprite color
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.white;
            }
        }
        #endregion

        #region Speech
        public void ShowSpeech(string text)
        {
            if (_speechBubble == null || !IsAlive) return;

            _speechBubble.Setup(text);
            Debug.Log($"[AgentVisual] {_currentData?.name} says: \"{text}\"");
        }

        public void HideSpeech()
        {
            if (_speechBubble != null)
            {
                _speechBubble.Hide();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Set the character sprite at runtime.
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            characterSprite = sprite;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
                _spriteRenderer.color = spriteColor;
            }
        }

        /// <summary>
        /// Set the character color (for placeholder or tinting).
        /// </summary>
        public void SetColor(Color bodyColor, Color outlineColor)
        {
            placeholderBodyColor = bodyColor;
            placeholderOutlineColor = outlineColor;

            if (characterSprite == null)
            {
                RegeneratePlaceholderSprite();
            }
            else
            {
                _spriteRenderer.color = bodyColor;
            }
        }
        #endregion
    }
}
