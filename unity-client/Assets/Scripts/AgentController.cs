using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Models;
using TheIsland.Network;

namespace TheIsland.Agents
{
    /// <summary>
    /// Controller for individual Agent prefabs.
    /// Handles UI updates, animations, and speech bubbles.
    /// </summary>
    public class AgentController : MonoBehaviour
    {
        #region UI References
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI personalityLabel;
        [SerializeField] private Slider hpBar;
        [SerializeField] private Slider energyBar;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image energyFill;

        [Header("Speech Bubble")]
        [SerializeField] private GameObject speechBubble;
        [SerializeField] private TextMeshProUGUI speechText;
        [SerializeField] private float speechDuration = 5f;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer characterRenderer;
        [SerializeField] private Color aliveColor = Color.white;
        [SerializeField] private Color deadColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private GameObject deathOverlay;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        #endregion

        #region Private Fields
        private int _agentId;
        private AgentData _currentData;
        private Coroutine _speechCoroutine;
        private Material _characterMaterial;
        #endregion

        #region Properties
        public int AgentId => _agentId;
        public AgentData CurrentData => _currentData;
        public bool IsAlive => _currentData?.IsAlive ?? false;
        #endregion

        #region Initialization
        private void Awake()
        {
            // Cache material for color changes
            if (characterRenderer != null)
            {
                _characterMaterial = characterRenderer.material;
            }

            // Hide speech bubble initially
            if (speechBubble != null)
            {
                speechBubble.SetActive(false);
            }

            // Hide death overlay initially
            if (deathOverlay != null)
            {
                deathOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// Initialize the agent with server data.
        /// Called once when the agent is first spawned.
        /// </summary>
        public void Initialize(AgentData data)
        {
            _agentId = data.id;
            _currentData = data;

            // Set static labels
            if (nameLabel != null)
            {
                nameLabel.text = data.name;
            }

            if (personalityLabel != null)
            {
                personalityLabel.text = data.personality;
            }

            // Set game object name for debugging
            gameObject.name = $"Agent_{data.id}_{data.name}";

            // Apply initial stats
            UpdateStats(data);

            Debug.Log($"[AgentController] Initialized {data.name} (ID: {data.id})");
        }
        #endregion

        #region Stats Update
        /// <summary>
        /// Update the agent's visual state based on server data.
        /// </summary>
        public void UpdateStats(AgentData data)
        {
            _currentData = data;

            // Update HP bar
            if (hpBar != null)
            {
                hpBar.value = data.hp / 100f;
                UpdateBarColor(hpFill, data.hp);
            }

            // Update Energy bar
            if (energyBar != null)
            {
                energyBar.value = data.energy / 100f;
                UpdateBarColor(energyFill, data.energy, isEnergy: true);
            }

            // Handle death state
            if (!data.IsAlive)
            {
                OnDeath();
            }
            else
            {
                OnAlive();
            }

            // Trigger animation based on energy level
            UpdateAnimation();
        }

        private void UpdateBarColor(Image fillImage, int value, bool isEnergy = false)
        {
            if (fillImage == null) return;

            if (isEnergy)
            {
                // Energy: Yellow to Orange gradient
                fillImage.color = Color.Lerp(
                    new Color(1f, 0.5f, 0f), // Orange (low)
                    new Color(1f, 0.8f, 0f), // Yellow (high)
                    value / 100f
                );
            }
            else
            {
                // HP: Red to Green gradient
                fillImage.color = Color.Lerp(
                    new Color(1f, 0.2f, 0.2f), // Red (low)
                    new Color(0.2f, 1f, 0.2f), // Green (high)
                    value / 100f
                );
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            if (_currentData == null) return;

            // Set animator parameters based on state
            animator.SetBool("IsAlive", _currentData.IsAlive);
            animator.SetFloat("Energy", _currentData.energy / 100f);
            animator.SetFloat("HP", _currentData.hp / 100f);

            // Trigger low energy animation if starving
            if (_currentData.energy <= 20 && _currentData.IsAlive)
            {
                animator.SetBool("IsStarving", true);
            }
            else
            {
                animator.SetBool("IsStarving", false);
            }
        }
        #endregion

        #region Death Handling
        private void OnDeath()
        {
            Debug.Log($"[AgentController] {_currentData.name} has died!");

            // Change character color to gray
            if (_characterMaterial != null)
            {
                _characterMaterial.color = deadColor;
            }

            // Show death overlay
            if (deathOverlay != null)
            {
                deathOverlay.SetActive(true);
            }

            // Trigger death animation
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            // Hide speech bubble
            HideSpeech();
        }

        private void OnAlive()
        {
            // Restore character color
            if (_characterMaterial != null)
            {
                _characterMaterial.color = aliveColor;
            }

            // Hide death overlay
            if (deathOverlay != null)
            {
                deathOverlay.SetActive(false);
            }
        }
        #endregion

        #region Speech Bubble
        /// <summary>
        /// Show speech bubble with text from LLM.
        /// Auto-hides after speechDuration seconds.
        /// </summary>
        public void ShowSpeech(string text)
        {
            if (speechBubble == null || speechText == null)
            {
                Debug.LogWarning($"[AgentController] Speech bubble not configured for {_currentData?.name}");
                return;
            }

            // Don't show speech for dead agents
            if (!IsAlive)
            {
                return;
            }

            // Stop existing speech coroutine if any
            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
            }

            // Set text and show bubble
            speechText.text = text;
            speechBubble.SetActive(true);

            // Start auto-hide coroutine
            _speechCoroutine = StartCoroutine(HideSpeechAfterDelay());

            Debug.Log($"[AgentController] {_currentData?.name} says: \"{text}\"");
        }

        private IEnumerator HideSpeechAfterDelay()
        {
            yield return new WaitForSeconds(speechDuration);
            HideSpeech();
        }

        public void HideSpeech()
        {
            if (speechBubble != null)
            {
                speechBubble.SetActive(false);
            }

            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
                _speechCoroutine = null;
            }
        }
        #endregion

        #region Interaction
        /// <summary>
        /// Called when player clicks/taps on this agent.
        /// </summary>
        public void OnClick()
        {
            if (!IsAlive)
            {
                Debug.Log($"[AgentController] Cannot interact with dead agent: {_currentData?.name}");
                return;
            }

            // Feed the agent
            NetworkManager.Instance.FeedAgent(_currentData.name);
        }

        private void OnMouseDown()
        {
            OnClick();
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            // Clean up material instance
            if (_characterMaterial != null)
            {
                Destroy(_characterMaterial);
            }
        }
        #endregion
    }
}
