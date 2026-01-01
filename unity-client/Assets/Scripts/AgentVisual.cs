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

        [Header("Mood Colors")]
        [SerializeField] private Color moodHappyColor = new Color(0.3f, 0.9f, 0.5f);
        [SerializeField] private Color moodNeutralColor = new Color(0.98f, 0.75f, 0.15f);
        [SerializeField] private Color moodSadColor = new Color(0.4f, 0.65f, 0.98f);
        [SerializeField] private Color moodAnxiousColor = new Color(0.97f, 0.53f, 0.53f);
        #endregion

        #region References
        private SpriteRenderer _spriteRenderer;
        private Canvas _uiCanvas;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _personalityLabel;
        private Image _hpBarFill;
        private Image _energyBarFill;
        private Image _moodBarFill;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _energyText;
        private TextMeshProUGUI _moodText;
        private TextMeshProUGUI _moodEmoji;
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

        // Animation state
        private float _idleAnimTimer;
        private float _breathScale = 1f;
        private Vector3 _originalSpriteScale;
        private float _bobOffset;
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

        private void Update()
        {
            if (!IsAlive) return;

            // Idle breathing animation
            _idleAnimTimer += Time.deltaTime;
            _breathScale = 1f + Mathf.Sin(_idleAnimTimer * 2f) * 0.02f;

            // Gentle bobbing
            _bobOffset = Mathf.Sin(_idleAnimTimer * 1.5f) * 0.05f;

            if (_spriteRenderer != null && _originalSpriteScale != Vector3.zero)
            {
                // Apply breathing scale
                _spriteRenderer.transform.localScale = new Vector3(
                    _originalSpriteScale.x * _breathScale,
                    _originalSpriteScale.y * _breathScale,
                    _originalSpriteScale.z
                );

                // Apply bobbing
                var pos = _spriteRenderer.transform.localPosition;
                pos.y = 1f + _bobOffset;
                _spriteRenderer.transform.localPosition = pos;
            }
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

            // Store original scale for animation
            _originalSpriteScale = spriteObj.transform.localScale;

            // Add billboard
            _spriteBillboard = spriteObj.AddComponent<Billboard>();

            // Add shadow
            CreateShadow(spriteObj.transform);
        }

        private void CreateShadow(Transform spriteTransform)
        {
            var shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(transform);
            shadowObj.transform.localPosition = new Vector3(0, 0.01f, 0);
            shadowObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shadowObj.transform.localScale = new Vector3(1.2f, 0.6f, 1f);

            var shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = CreateShadowSprite();
            shadowRenderer.sortingOrder = sortingOrder - 1;
            shadowRenderer.color = new Color(0, 0, 0, 0.3f);
        }

        private Sprite CreateShadowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center.x) / (size * 0.4f);
                    float dy = (y - center.y) / (size * 0.4f);
                    float dist = dx * dx + dy * dy;

                    if (dist < 1)
                    {
                        float alpha = Mathf.Clamp01(1 - dist) * 0.5f;
                        pixels[y * size + x] = new Color(0, 0, 0, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
            texture.filterMode = FilterMode.Bilinear;

            // Clear to transparent
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            Vector2 center = new Vector2(width / 2f, height / 2f);

            // Create highlight and shadow colors
            Color highlight = Color.Lerp(placeholderBodyColor, Color.white, 0.3f);
            Color shadow = Color.Lerp(placeholderBodyColor, Color.black, 0.3f);
            Color skinTone = new Color(0.95f, 0.8f, 0.7f);
            Color skinShadow = new Color(0.85f, 0.65f, 0.55f);

            // Body (ellipse with shading)
            Vector2 bodyCenter = center + Vector2.down * 6;
            DrawShadedEllipse(pixels, width, height, bodyCenter, 16, 22, placeholderBodyColor, highlight, shadow);

            // Head (circle with skin tone)
            Vector2 headCenter = center + Vector2.up * 14;
            DrawShadedCircle(pixels, width, height, headCenter, 13, skinTone, Color.Lerp(skinTone, Color.white, 0.2f), skinShadow);

            // Hair (top of head)
            Color hairColor = placeholderOutlineColor;
            DrawHair(pixels, width, height, headCenter, 13, hairColor);

            // Eyes
            DrawCircle(pixels, width, height, headCenter + new Vector2(-4, -1), 3, Color.white);
            DrawCircle(pixels, width, height, headCenter + new Vector2(4, -1), 3, Color.white);
            DrawCircle(pixels, width, height, headCenter + new Vector2(-4, -1), 1.5f, new Color(0.2f, 0.15f, 0.1f));
            DrawCircle(pixels, width, height, headCenter + new Vector2(4, -1), 1.5f, new Color(0.2f, 0.15f, 0.1f));
            // Eye highlights
            DrawCircle(pixels, width, height, headCenter + new Vector2(-3, 0), 0.8f, Color.white);
            DrawCircle(pixels, width, height, headCenter + new Vector2(5, 0), 0.8f, Color.white);

            // Mouth (smile)
            DrawSmile(pixels, width, height, headCenter + Vector2.down * 5, 4);

            // Blush
            DrawCircle(pixels, width, height, headCenter + new Vector2(-7, -3), 2, new Color(1f, 0.6f, 0.6f, 0.4f));
            DrawCircle(pixels, width, height, headCenter + new Vector2(7, -3), 2, new Color(1f, 0.6f, 0.6f, 0.4f));

            // Arms
            DrawArm(pixels, width, height, bodyCenter + new Vector2(-14, 5), -30, skinTone);
            DrawArm(pixels, width, height, bodyCenter + new Vector2(14, 5), 30, skinTone);

            // Legs
            DrawLeg(pixels, width, height, bodyCenter + new Vector2(-6, -20), placeholderBodyColor);
            DrawLeg(pixels, width, height, bodyCenter + new Vector2(6, -20), placeholderBodyColor);

            // Outline
            AddOutline(pixels, width, height, placeholderOutlineColor);

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawShadedCircle(Color[] pixels, int width, int height, Vector2 center, float radius, Color baseColor, Color highlight, Color shadow)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        // Shading based on position relative to light source (top-left)
                        float dx = (x - center.x) / radius;
                        float dy = (y - center.y) / radius;
                        float shade = (-dx * 0.3f + dy * 0.7f) * 0.5f + 0.5f;
                        Color color = Color.Lerp(highlight, shadow, shade);
                        color = Color.Lerp(color, baseColor, 0.5f);
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void DrawShadedEllipse(Color[] pixels, int width, int height, Vector2 center, float rx, float ry, Color baseColor, Color highlight, Color shadow)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - center.x) / rx;
                    float dy = (y - center.y) / ry;
                    if (dx * dx + dy * dy <= 1)
                    {
                        float shade = (-dx * 0.3f + dy * 0.5f) * 0.5f + 0.5f;
                        Color color = Color.Lerp(highlight, shadow, shade);
                        color = Color.Lerp(color, baseColor, 0.5f);
                        pixels[y * width + x] = color;
                    }
                }
            }
        }

        private void DrawHair(Color[] pixels, int width, int height, Vector2 headCenter, float headRadius, Color hairColor)
        {
            // Draw hair on top half of head
            for (int y = (int)(headCenter.y); y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), headCenter);
                    if (dist <= headRadius + 2 && dist >= headRadius - 4 && y > headCenter.y - 2)
                    {
                        float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                        if (noise > 0.3f)
                        {
                            pixels[y * width + x] = Color.Lerp(hairColor, hairColor * 0.7f, noise);
                        }
                    }
                }
            }
        }

        private void DrawSmile(Color[] pixels, int width, int height, Vector2 center, float smileWidth)
        {
            Color mouthColor = new Color(0.8f, 0.4f, 0.4f);
            for (int x = (int)(center.x - smileWidth); x <= (int)(center.x + smileWidth); x++)
            {
                float t = (x - center.x + smileWidth) / (smileWidth * 2);
                int y = (int)(center.y - Mathf.Sin(t * Mathf.PI) * 2);
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    pixels[y * width + x] = mouthColor;
                    if (y > 0) pixels[(y - 1) * width + x] = mouthColor;
                }
            }
        }

        private void DrawArm(Color[] pixels, int width, int height, Vector2 start, float angle, Color skinColor)
        {
            float rad = angle * Mathf.Deg2Rad;
            int length = 10;
            for (int i = 0; i < length; i++)
            {
                int x = (int)(start.x + Mathf.Sin(rad) * i);
                int y = (int)(start.y - Mathf.Cos(rad) * i);
                DrawCircle(pixels, width, height, new Vector2(x, y), 2, skinColor);
            }
        }

        private void DrawLeg(Color[] pixels, int width, int height, Vector2 start, Color clothColor)
        {
            for (int i = 0; i < 8; i++)
            {
                int x = (int)start.x;
                int y = (int)(start.y - i);
                if (y >= 0 && y < height)
                {
                    DrawCircle(pixels, width, height, new Vector2(x, y), 3, clothColor);
                }
            }
            // Shoe
            DrawCircle(pixels, width, height, start + Vector2.down * 8, 4, new Color(0.3f, 0.2f, 0.15f));
        }

        private void AddOutline(Color[] pixels, int width, int height, Color outlineColor)
        {
            Color[] newPixels = (Color[])pixels.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (pixels[y * width + x].a < 0.1f)
                    {
                        // Check neighbors
                        bool hasNeighbor = false;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (pixels[(y + dy) * width + (x + dx)].a > 0.5f)
                                {
                                    hasNeighbor = true;
                                    break;
                                }
                            }
                            if (hasNeighbor) break;
                        }
                        if (hasNeighbor)
                        {
                            newPixels[y * width + x] = outlineColor;
                        }
                    }
                }
            }
            System.Array.Copy(newPixels, pixels, pixels.Length);
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
            canvasRect.sizeDelta = new Vector2(400, 180);

            // Add billboard to canvas (configured for UI - full facing)
            _uiBillboard = canvasObj.AddComponent<Billboard>();
            _uiBillboard.ConfigureForUI();

            // Create UI panel (increased height for mood bar)
            var panel = CreateUIPanel(canvasObj.transform, new Vector2(350, 150));

            // Name label
            _nameLabel = CreateUIText(panel.transform, "NameLabel", "Agent", 36, Color.white, FontStyles.Bold);
            SetRectPosition(_nameLabel.rectTransform, 0, 60, 320, 45);

            // Personality label
            _personalityLabel = CreateUIText(panel.transform, "PersonalityLabel", "(Personality)", 20,
                new Color(0.8f, 0.8f, 0.8f), FontStyles.Italic);
            SetRectPosition(_personalityLabel.rectTransform, 0, 30, 320, 25);

            // HP Bar
            var hpBar = CreateProgressBar(panel.transform, "HPBar", "HP", hpHighColor, out _hpBarFill, out _hpText);
            SetRectPosition(hpBar, 0, 0, 280, 24);

            // Energy Bar
            var energyBar = CreateProgressBar(panel.transform, "EnergyBar", "Energy", energyHighColor, out _energyBarFill, out _energyText);
            SetRectPosition(energyBar, 0, -30, 280, 24);

            // Mood Bar
            var moodBar = CreateProgressBar(panel.transform, "MoodBar", "Mood", moodNeutralColor, out _moodBarFill, out _moodText);
            SetRectPosition(moodBar, 0, -60, 280, 24);

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
            bg.sprite = CreateRoundedRectSprite(32, 32, 8);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);

            // Add subtle border
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(panel.transform);
            borderObj.transform.localPosition = Vector3.zero;
            borderObj.transform.localRotation = Quaternion.identity;
            borderObj.transform.localScale = Vector3.one;

            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            borderRect.SetAsFirstSibling();

            var borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = CreateRoundedRectSprite(32, 32, 8);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.3f, 0.35f, 0.45f, 0.5f);

            return panel;
        }

        private Sprite CreateRoundedRectSprite(int width, int height, int radius)
        {
            Texture2D tex = new Texture2D(width, height);
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inRect = true;

                    // Check corners for rounding
                    if (x < radius && y < radius)
                    {
                        // Bottom-left corner
                        inRect = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) <= radius;
                    }
                    else if (x >= width - radius && y < radius)
                    {
                        // Bottom-right corner
                        inRect = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) <= radius;
                    }
                    else if (x < radius && y >= height - radius)
                    {
                        // Top-left corner
                        inRect = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) <= radius;
                    }
                    else if (x >= width - radius && y >= height - radius)
                    {
                        // Top-right corner
                        inRect = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) <= radius;
                    }

                    pixels[y * width + x] = inRect ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Create 9-sliced sprite
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
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

            // Update Mood bar
            float moodPercent = data.mood / 100f;
            if (_moodBarFill != null)
            {
                _moodBarFill.rectTransform.anchorMax = new Vector2(moodPercent, 1);
                _moodBarFill.color = GetMoodColor(data.mood_state);
            }
            if (_moodText != null)
            {
                string moodIndicator = GetMoodEmoji(data.mood_state);
                string moodLabel = data.mood_state switch
                {
                    "happy" => "Happy",
                    "sad" => "Sad",
                    "anxious" => "Anxious",
                    _ => "Neutral"
                };
                _moodText.text = $"{moodIndicator} {moodLabel}: {data.mood}";
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

        private Color GetMoodColor(string moodState)
        {
            return moodState switch
            {
                "happy" => moodHappyColor,
                "sad" => moodSadColor,
                "anxious" => moodAnxiousColor,
                _ => moodNeutralColor
            };
        }

        private string GetMoodEmoji(string moodState)
        {
            // Use text symbols instead of emoji for font compatibility
            return moodState switch
            {
                "happy" => "<color=#5AE65A>+</color>",
                "sad" => "<color=#6AA8FF>-</color>",
                "anxious" => "<color=#FF7777>!</color>",
                _ => "<color=#FFD700>~</color>"
            };
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
            ShowSpeech(text, speechDuration);
        }

        public void ShowSpeech(string text, float duration)
        {
            if (_speechBubble == null || !IsAlive) return;

            _speechBubble.DisplayDuration = duration;
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
