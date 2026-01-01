using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheIsland.Visual
{
    /// <summary>
    /// Enhanced speech bubble with pop-in animation and auto-hide.
    /// Can be used as a standalone prefab or created programmatically.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        #region Configuration
        [Header("Visual Settings")]
        [SerializeField] private float maxWidth = 350f;
        [SerializeField] private float padding = 20f;
        [SerializeField] private Color bubbleColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color textColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Animation Settings")]
        [SerializeField] private float popInDuration = 0.25f;
        [SerializeField] private float displayDuration = 5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private AnimationCurve popInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Bounce Effect")]
        [SerializeField] private bool enableBounce = true;
        [SerializeField] private float bounceScale = 1.1f;
        [SerializeField] private float bounceBackDuration = 0.1f;

        [Header("Typewriter Effect")]
        [SerializeField] private bool enableTypewriter = false;
        [SerializeField] private float typewriterSpeed = 30f; // characters per second
        #endregion

        #region UI References
        private RectTransform _rectTransform;
        private Image _bubbleBackground;
        private Image _bubbleOutline;
        private TextMeshProUGUI _textComponent;
        private GameObject _tailObject;
        private CanvasGroup _canvasGroup;
        #endregion

        #region State
        private Coroutine _currentAnimation;
        private Coroutine _autoHideCoroutine;
        private string _fullText;
        private bool _isShowing;
        #endregion

        #region Properties
        public bool IsShowing => _isShowing;
        public float DisplayDuration
        {
            get => displayDuration;
            set => displayDuration = value;
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CreateBubbleUI();
            // Start hidden
            transform.localScale = Vector3.zero;
            _isShowing = false;
        }
        #endregion

        #region UI Creation
        private void CreateBubbleUI()
        {
            // Ensure we have a RectTransform
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }
            _rectTransform.sizeDelta = new Vector2(maxWidth, 80);

            // Add CanvasGroup for fading
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Create outline (slightly larger background)
            var outlineObj = new GameObject("Outline");
            outlineObj.transform.SetParent(transform);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localRotation = Quaternion.identity;
            outlineObj.transform.localScale = Vector3.one;

            _bubbleOutline = outlineObj.AddComponent<Image>();
            _bubbleOutline.color = outlineColor;
            var outlineRect = outlineObj.GetComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = new Vector2(-3, -3);
            outlineRect.offsetMax = new Vector2(3, 3);

            // Create main background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localRotation = Quaternion.identity;
            bgObj.transform.localScale = Vector3.one;

            _bubbleBackground = bgObj.AddComponent<Image>();
            _bubbleBackground.color = bubbleColor;
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Create text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            _textComponent = textObj.AddComponent<TextMeshProUGUI>();
            _textComponent.fontSize = 22;
            _textComponent.color = textColor;
            _textComponent.alignment = TextAlignmentOptions.Center;
            _textComponent.textWrappingMode = TextWrappingModes.Normal;
            _textComponent.overflowMode = TextOverflowModes.Ellipsis;
            _textComponent.margin = new Vector4(padding, padding * 0.5f, padding, padding * 0.5f);

            var textRect = _textComponent.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Create tail (triangle pointing down)
            _tailObject = CreateTail();
        }

        private GameObject CreateTail()
        {
            var tail = new GameObject("Tail");
            tail.transform.SetParent(transform);
            tail.transform.localRotation = Quaternion.identity;
            tail.transform.localScale = Vector3.one;

            var tailRect = tail.AddComponent<RectTransform>();
            tailRect.anchorMin = new Vector2(0.5f, 0);
            tailRect.anchorMax = new Vector2(0.5f, 0);
            tailRect.pivot = new Vector2(0.5f, 1);
            tailRect.anchoredPosition = new Vector2(0, 0);
            tailRect.sizeDelta = new Vector2(24, 16);

            // Create a simple triangle using UI Image with a sprite
            // For now, use a simple downward-pointing shape
            var tailImage = tail.AddComponent<Image>();
            tailImage.color = bubbleColor;

            // Note: For a proper triangle, you'd use a custom sprite.
            // This creates a simple rectangle as placeholder.
            // In production, replace with a triangle sprite.

            return tail;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Setup and show the speech bubble with the given text.
        /// </summary>
        public void Setup(string text)
        {
            _fullText = text;

            // Stop any existing animations
            StopAllAnimations();

            // Set text (either immediate or typewriter)
            if (enableTypewriter)
            {
                _textComponent.text = "";
                StartCoroutine(TypewriterEffect(text));
            }
            else
            {
                _textComponent.text = text;
            }

            // Auto-size the bubble based on text
            AdjustSizeToContent();

            // Start show animation
            _currentAnimation = StartCoroutine(PopInAnimation());

            // Schedule auto-hide
            _autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());

            _isShowing = true;
            Debug.Log($"[SpeechBubble] Showing: \"{text}\"");
        }

        /// <summary>
        /// Immediately hide the bubble.
        /// </summary>
        public void Hide()
        {
            StopAllAnimations();
            StartCoroutine(FadeOutAnimation());
        }

        /// <summary>
        /// Update bubble colors at runtime.
        /// </summary>
        public void SetColors(Color bubble, Color text, Color outline)
        {
            bubbleColor = bubble;
            textColor = text;
            outlineColor = outline;

            if (_bubbleBackground != null) _bubbleBackground.color = bubbleColor;
            if (_textComponent != null) _textComponent.color = textColor;
            if (_bubbleOutline != null) _bubbleOutline.color = outlineColor;
        }
        #endregion

        #region Animations
        private IEnumerator PopInAnimation()
        {
            float elapsed = 0f;
            _canvasGroup.alpha = 1f;

            // Pop in from zero to slightly larger than target
            while (elapsed < popInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popInDuration;
                float curveValue = popInCurve.Evaluate(t);

                float targetScale = enableBounce ? bounceScale : 1f;
                transform.localScale = Vector3.one * (curveValue * targetScale);

                yield return null;
            }

            // Bounce back to normal size
            if (enableBounce)
            {
                elapsed = 0f;
                while (elapsed < bounceBackDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / bounceBackDuration;
                    float scale = Mathf.Lerp(bounceScale, 1f, t);
                    transform.localScale = Vector3.one * scale;
                    yield return null;
                }
            }

            transform.localScale = Vector3.one;
            _currentAnimation = null;
        }

        private IEnumerator FadeOutAnimation()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;
            Vector3 startScale = transform.localScale;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                yield return null;
            }

            _canvasGroup.alpha = 0f;
            transform.localScale = Vector3.zero;
            _isShowing = false;
            _currentAnimation = null;
        }

        private IEnumerator TypewriterEffect(string text)
        {
            int charCount = 0;
            float timer = 0f;
            float charInterval = 1f / typewriterSpeed;

            while (charCount < text.Length)
            {
                timer += Time.deltaTime;

                while (timer >= charInterval && charCount < text.Length)
                {
                    timer -= charInterval;
                    charCount++;
                    _textComponent.text = text.Substring(0, charCount);

                    // Re-adjust size as text grows
                    AdjustSizeToContent();
                }

                yield return null;
            }

            _textComponent.text = text;
        }

        private IEnumerator AutoHideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            Hide();
        }
        #endregion

        #region Helpers
        private void StopAllAnimations()
        {
            if (_currentAnimation != null)
            {
                StopCoroutine(_currentAnimation);
                _currentAnimation = null;
            }

            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
        }

        private void AdjustSizeToContent()
        {
            if (_textComponent == null || _rectTransform == null) return;

            // Force mesh update to get accurate preferred values
            _textComponent.ForceMeshUpdate();

            // Get preferred size
            Vector2 preferredSize = _textComponent.GetPreferredValues();

            // Add padding
            float width = Mathf.Min(preferredSize.x + padding * 2, maxWidth);
            float height = preferredSize.y + padding;

            // If text is wider than max, recalculate height for wrapped text
            if (preferredSize.x > maxWidth - padding * 2)
            {
                _textComponent.ForceMeshUpdate();
                height = _textComponent.GetPreferredValues(maxWidth - padding * 2, 0).y + padding;
                width = maxWidth;
            }

            _rectTransform.sizeDelta = new Vector2(width, height);
        }
        #endregion

        #region Static Factory
        /// <summary>
        /// Create a speech bubble as a child of the specified parent.
        /// </summary>
        public static SpeechBubble Create(Transform parent, Vector3 localPosition)
        {
            var bubbleObj = new GameObject("SpeechBubble");
            bubbleObj.transform.SetParent(parent);
            bubbleObj.transform.localPosition = localPosition;
            bubbleObj.transform.localRotation = Quaternion.identity;
            bubbleObj.transform.localScale = Vector3.one;

            var bubble = bubbleObj.AddComponent<SpeechBubble>();
            return bubble;
        }
        #endregion
    }
}
