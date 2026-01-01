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
        private AgentAnimator _animator;
        #endregion

        #region State
        private int _agentId;
        private AgentData _currentData;
        private string _moodState = "neutral";
        private Coroutine _speechCoroutine;

        // Animation state
        private float _idleAnimTimer;
        private Vector3 _originalSpriteScale;
        private float _bobOffset;

        // Movement state
        private Vector3 _targetPosition;
        private bool _isMoving;
        private float _moveSpeed = 3f;
        private Vector3 _lastPosition;
        private GameObject _shadowObj;
        private SpriteRenderer _shadowRenderer;
        private float _footstepTimer;

        // UI Smoothing (Phase 19)
        private float _currentHpPercent;
        private float _currentEnergyPercent;
        private float _currentMoodPercent;
        private float _targetHpPercent;
        private float _targetEnergyPercent;
        private float _targetMoodPercent;
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
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            // Phase 19-B: Ensure AgentAnimator is present
            _animator = GetComponent<AgentAnimator>();
            if (_animator == null) _animator = gameObject.AddComponent<AgentAnimator>();
            
            CreateVisuals();
            CreateShadow();
            _lastPosition = transform.position;
        }

        private void CreateShadow()
        {
            _shadowObj = new GameObject("Shadow");
            _shadowObj.transform.SetParent(transform);
            _shadowObj.transform.localPosition = new Vector3(0, 0.05f, 0); // Slightly above ground
            _shadowObj.transform.localRotation = Quaternion.Euler(90, 0, 0); // Flat on ground
            
            _shadowRenderer = _shadowObj.AddComponent<SpriteRenderer>();
            _shadowRenderer.sprite = CreateBlobShadowSprite();
            _shadowRenderer.color = new Color(0, 0, 0, 0.3f);
            _shadowRenderer.sortingOrder = 1; // Just above ground
        }

        private Sprite CreateBlobShadowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float alpha = Mathf.Exp(-dist * 4f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (!IsAlive) return;

            // Phase 19-D: Apply soft-repulsion to prevent crowding
            Vector3 repulsion = CalculateRepulsion();
            
            // Handle Movement
            if (_isMoving)
            {
                // Simple steering toward target
                Vector3 moveDir = (_targetPosition - transform.position).normalized;
                Vector3 finalVelocity = (moveDir * _moveSpeed) + repulsion;
                
                transform.position += finalVelocity * Time.deltaTime;

                // Flip sprite based on direction
                if (_spriteRenderer != null && Mathf.Abs(moveDir.x) > 0.01f)
                {
                    _spriteRenderer.flipX = moveDir.x < 0;
                }

                if (Vector3.Distance(transform.position, _targetPosition) < 0.1f)
                {
                    _isMoving = false;
                }
            }
            else if (repulsion.sqrMagnitude > 0.001f)
            {
                // Push away even when idle
                transform.position += repulsion * Time.deltaTime;
            }

            // Phase 19-D: Dynamic Z-Sorting
            if (_spriteRenderer != null)
            {
                // In world space, higher Z (further) should have lower sorting order
                // Z typically ranges from -10 to 10 on the island
                _spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.z * 100);
            }

            // Phase 19-B/D/E: Use AgentAnimator
            if (_animator != null)
            {
                // Calculate world velocity based on position change
                Vector3 currentVelocity = (transform.position - _lastPosition) / (Time.deltaTime > 0 ? Time.deltaTime : 0.001f);
                _animator.SetMovement(currentVelocity, _moveSpeed);
                _lastPosition = transform.position;
            }

            // Phase 19-E: Social Orientation (Interaction Facing)
            if (!_isMoving)
            {
                FaceInteractionTarget();
            }

            // Phase 19-F: AAA Grounding (Shadow & Footsteps)
            UpdateGrounding();

            // Phase 19-F: Random emotion trigger test (Optional, for demo)
            if (Random.value < 0.001f) ShowEmotion("!"); 

            // Phase 19: Smooth UI Bar Transitions
            UpdateSmoothBars();
        }

        private void UpdateGrounding()
        {
            if (_shadowObj != null)
            {
                // Shadow follows but stays on ground (assuming Y=0 is ground level or character is at Y=0)
                // For simplicity, we just keep it at local zero.
                // If the character bops up, the shadow should shrink slightly
                float bopY = (_spriteRenderer != null) ? _spriteRenderer.transform.localPosition.y : 0;
                float shadowScale = Mathf.Clamp(1.0f - (bopY * 0.5f), 0.5f, 1.2f);
                _shadowObj.transform.localScale = new Vector3(1.2f * shadowScale, 0.6f * shadowScale, 1f);
            }

            if (_isMoving)
            {
                _footstepTimer += Time.deltaTime;
                if (_footstepTimer > 0.35f) // Approximate footstep interval
                {
                    _footstepTimer = 0;
                    if (TheIsland.Visual.VisualEffectsManager.Instance != null)
                    {
                        TheIsland.Visual.VisualEffectsManager.Instance.SpawnFootstepDust(transform.position);
                    }
                }
            }
        }

        private void FaceInteractionTarget()
        {
            // If the agent is talking or near others, turn to face them
            float socialRange = 2.5f;
            AgentVisual nearestAgent = null;
            float minDist = socialRange;

            var allAgents = FindObjectsByType<AgentVisual>(FindObjectsSortMode.None);
            foreach (var other in allAgents)
            {
                if (other == this || !other.IsAlive) continue;
                float d = Vector3.Distance(transform.position, other.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearestAgent = other;
                }
            }

            if (nearestAgent != null && _spriteRenderer != null)
            {
                float dx = nearestAgent.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.1f)
                {
                    _spriteRenderer.flipX = dx < 0;
                }
            }
        }

        public void ShowEmotion(string type)
        {
            var bubble = new GameObject("EmotionBubble");
            bubble.transform.SetParent(transform);
            bubble.transform.localPosition = new Vector3(0, 2.5f, 0); // Above head
            
            var sprite = bubble.AddComponent<SpriteRenderer>();
            sprite.sprite = CreateEmotionSprite(type);
            sprite.sortingOrder = 110; // Top layer
            bubble.AddComponent<Billboard>();
            
            StartCoroutine(AnimateEmotion(bubble));
        }

        private Sprite CreateEmotionSprite(string type)
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            Color bgColor = Color.white;
            Color iconColor = type == "!" ? Color.red : (type == "?" ? Color.blue : Color.black);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2.2f);
                    bool isBorder = d > 0.85f && d < 1.0f;
                    bool isBg = d <= 0.85f;
                    
                    if (isBorder) tex.SetPixel(x, y, Color.black);
                    else if (isBg) tex.SetPixel(x, y, bgColor);
                    else tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
            // Simple "!" or "?" pixel art logic could go here, but for now just a red dot for "!"
            if (type == "!") {
                for (int y = 10; y < 24; y++) tex.SetPixel(16, y, iconColor);
                tex.SetPixel(16, 8, iconColor);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private IEnumerator AnimateEmotion(GameObject bubble)
        {
            float elapsed = 0;
            float duration = 1.5f;
            Vector3 startScale = Vector3.zero;
            Vector3 peakScale = Vector3.one * 0.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = (t < 0.2f) ? (t / 0.2f) : (1f - (t - 0.2f) / 0.8f);
                bubble.transform.localScale = peakScale * (Mathf.Sin(t * Mathf.PI * 1.5f) * 0.2f + 0.8f);
                bubble.transform.localPosition += Vector3.up * Time.deltaTime * 0.2f;
                
                if (t > 0.8f) {
                    var s = bubble.GetComponent<SpriteRenderer>();
                    s.color = new Color(1, 1, 1, (1f - t) / 0.2f);
                }
                yield return null;
            }
            Destroy(bubble);
        }

        private Vector3 CalculateRepulsion()
        {
            Vector3 force = Vector3.zero;
            float radius = 1.2f; // Social distancing radius
            float strength = 1.5f;

            var allAgents = FindObjectsByType<AgentVisual>(FindObjectsSortMode.None);
            foreach (var other in allAgents)
            {
                if (other == this || !other.IsAlive) continue;

                Vector3 diff = transform.position - other.transform.position;
                float dist = diff.magnitude;

                if (dist < radius && dist > 0.01f)
                {
                    // Linear falloff repulsion
                    force += diff.normalized * (1.0f - (dist / radius)) * strength;
                }
            }
            return force;
        }

        private void UpdateSmoothBars()
        {
            float lerpSpeed = 5f * Time.deltaTime;
            
            if (_hpBarFill != null)
            {
                _currentHpPercent = Mathf.Lerp(_currentHpPercent, _targetHpPercent, lerpSpeed);
                _hpBarFill.rectTransform.anchorMax = new Vector2(_currentHpPercent, 1);
                _hpBarFill.color = Color.Lerp(hpLowColor, hpHighColor, _currentHpPercent);
            }

            if (_energyBarFill != null)
            {
                _currentEnergyPercent = Mathf.Lerp(_currentEnergyPercent, _targetEnergyPercent, lerpSpeed);
                _energyBarFill.rectTransform.anchorMax = new Vector2(_currentEnergyPercent, 1);
                _energyBarFill.color = Color.Lerp(energyLowColor, energyHighColor, _currentEnergyPercent);
            }

            if (_moodBarFill != null)
            {
                _currentMoodPercent = Mathf.Lerp(_currentMoodPercent, _targetMoodPercent, lerpSpeed);
                _moodBarFill.rectTransform.anchorMax = new Vector2(_currentMoodPercent, 1);
                _moodBarFill.color = GetMoodColor(_currentData?.mood_state ?? "neutral");
            }
        }

        // Trigger a jump animation (to be called by events)
        public void DoJump()
        {
            StartCoroutine(JumpRoutine());
        }

        public void MoveTo(Vector3 target)
        {
            _targetPosition = target;
            // Keep current Y (height) to avoid sinking/flying, unless target specifies it
            // Actually our agents are on navmesh or free moving? Free moving for now.
            // But we want to keep them on the "ground" plane roughly.
            // Let's preserve current Y if target Y is 0 (which usually means undefined in 2D topdown logic, but here we are 2.5D)
            // The spawn positions have Y=0.
            _targetPosition.y = transform.position.y;
            _isMoving = true;
        }

        private void MoveTowardsTarget()
        {
            Vector3 direction = (_targetPosition - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

            // Stop when close enough
            if (Vector3.Distance(transform.position, _targetPosition) < 0.1f)
            {
                _isMoving = false;
            }
        }

        private IEnumerator JumpRoutine()
        {
            float timer = 0;
            float duration = 0.4f;
            Vector3 startPos = _spriteRenderer.transform.localPosition;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                
                // Parabolic jump height
                float height = Mathf.Sin(t * Mathf.PI) * 0.5f;
                
                var pos = _spriteRenderer.transform.localPosition;
                pos.y = startPos.y + height;
                _spriteRenderer.transform.localPosition = pos;
                yield return null;
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

            // Loading premium assets (Phase 19)
            TryLoadPremiumSprite(data.id);

            // Apply unique color based on agent ID (as fallback/tint)
            ApplyAgentColor(data.id);

            // Set UI text
            if (_nameLabel != null) _nameLabel.text = data.name;
            if (_personalityLabel != null) _personalityLabel.text = $"({data.personality})";

            UpdateStats(data);
            Debug.Log($"[AgentVisual] Initialized: {data.name}");
        }

        private void TryLoadPremiumSprite(int id)
        {
            // Load the collection texture from Assets
            // Note: In a real build, we'd use Resources.Load or Addressables.
            // For this environment, we'll try to find it in the path or use a static reference.
            // Since we can't easily use Resources.Load at runtime for arbitrary paths, 
            // we'll implement a simple runtime texture loader if needed, or assume it's assigned to a manager.
            
            // For now, let's assume the texture is assigned or loaded.
            // I'll add a static reference to the collection texture in NetworkManager or AgentVisual.
            
            if (characterSprite != null) return; // Already has a sprite

            StartCoroutine(LoadSpriteCoroutine(id));
        }

        private IEnumerator LoadSpriteCoroutine(int id)
        {
            // This is a simplified runtime loader for the demonstration
            string path = Application.dataPath + "/Sprites/Characters.png";
            if (!System.IO.File.Exists(path)) yield break;

            byte[] fileData = System.IO.File.ReadAllBytes(path);
            Texture2D sourceTex = new Texture2D(2, 2);
            sourceTex.LoadImage(fileData);
            
            // Phase 19-C: Fix black/white background with robust transcoding
            Texture2D tex = ProcessTransparency(sourceTex);

            // Slice the 1x3 collection (3 characters in a row)
            int charIndex = id % 3;
            float charWidth = tex.width / 3f;
            Rect rect = new Rect(charIndex * charWidth, 0, charWidth, tex.height);
            
            characterSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = characterSprite;
                _spriteRenderer.color = Color.white;
                
                // Phase 19-C: Normalize scale. Target height approx 2.0 units.
                float spriteHeightUnits = characterSprite.rect.height / characterSprite.pixelsPerUnit;
                float normScale = 2.0f / spriteHeightUnits; // Desired height is 2.0 units
                _spriteRenderer.transform.localScale = new Vector3(normScale, normScale, 1);
                
                // Update original scale for animator
                _originalSpriteScale = _spriteRenderer.transform.localScale;
            }
        }

        private Texture2D ProcessTransparency(Texture2D source)
        {
            if (source == null) return null;

            // Create a new texture with Alpha channel
            Texture2D tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels();
            
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                // Chroma-key: If pixel is very close to white, make it transparent
                // Using 0.9f as threshold to catch almost-white artifacts
                if (p.r > 0.9f && p.g > 0.9f && p.b > 0.9f)
                {
                    pixels[i] = new Color(0, 0, 0, 0);
                }
                else
                {
                    // Ensure full opacity for others
                    pixels[i] = new Color(p.r, p.g, p.b, 1.0f);
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
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
                int y = (int)center.y;

                // Mouth shape based on mood
                if (_moodState == "happy")
                {
                    y = (int)(center.y - Mathf.Sin(t * Mathf.PI) * 2);
                }
                else if (_moodState == "sad")
                {
                    y = (int)(center.y - 2 + Mathf.Sin(t * Mathf.PI) * 2);
                }
                else if (_moodState == "anxious")
                {
                    // Wavy mouth
                    y = (int)(center.y + Mathf.Sin(t * Mathf.PI * 3) * 1);
                }
                else // neutral
                {
                    y = (int)(center.y);
                }

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
            bg.sprite = CreateRoundedRectSprite(32, 32, 12);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.08f, 0.15f, 0.45f); // Darker, more transparent glass

            // Add inner glow border (Phase 19)
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(panel.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(1, 1);
            borderRect.offsetMax = new Vector2(-1, -1);
            
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = CreateRoundedRectSprite(32, 32, 12);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(1f, 1f, 1f, 0.15f); // Subtle highlight

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

            // Set targets for smooth lerping (Phase 19)
            _targetHpPercent = data.hp / 100f;
            _targetEnergyPercent = data.energy / 100f;
            _targetMoodPercent = data.mood / 100f;

            if (_hpText != null)
            {
                _hpText.text = $"HP: {data.hp}";
            }

            if (_energyText != null)
            {
                _energyText.text = $"Energy: {data.energy}";
            }

            // Check for mood change (Visual Expression)
            if (_moodState != data.mood_state)
            {
                _moodState = data.mood_state;
                // Only regenerate if using placeholder sprite
                if (characterSprite == null && _spriteRenderer != null)
                {
                    RegeneratePlaceholderSprite();
                }
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

            // Restore sprite color based on state
            if (_spriteRenderer != null)
            {
                Color targetColor = spriteColor;

                // Phase 15: Sickness visual (Green tint)
                if (_currentData != null && _currentData.is_sick)
                {
                    targetColor = Color.Lerp(targetColor, Color.green, 0.4f);
                }

                _spriteRenderer.color = targetColor;
            }

            // Phase 17-B: Update social role display
            UpdateSocialRoleDisplay();
        }

        /// <summary>
        /// Display social role indicator based on agent's role.
        /// </summary>
        private void UpdateSocialRoleDisplay()
        {
            if (_currentData == null || _nameLabel == null) return;

            string roleIcon = _currentData.social_role switch
            {
                "leader" => " <color=#FFD700>★</color>",    // Gold star
                "loner" => " <color=#808080>☁</color>",    // Gray cloud
                "follower" => " <color=#87CEEB>→</color>", // Sky blue arrow
                _ => ""
            };

            // Append role icon to name (strip any existing icons first)
            string baseName = _currentData.name;
            _nameLabel.text = baseName + roleIcon;
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
