using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Models;
using TheIsland.Network;

namespace TheIsland.UI
{
    /// <summary>
    /// Cinematic narrative UI overlay for AI Director events and voting.
    /// Handles plot cards, voting bars, and resolution displays.
    /// </summary>
    public class NarrativeUI : MonoBehaviour
    {
        #region Singleton
        private static NarrativeUI _instance;
        public static NarrativeUI Instance => _instance;
        #endregion

        #region UI References
        [Header("Main Panel")]
        [SerializeField] private CanvasGroup mainPanel;
        [SerializeField] private Image backgroundOverlay;

        [Header("Event Card")]
        [SerializeField] private RectTransform eventCard;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Voting Panel")]
        [SerializeField] private RectTransform votingPanel;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI totalVotesText;

        [Header("Choice A")]
        [SerializeField] private RectTransform choiceAContainer;
        [SerializeField] private TextMeshProUGUI choiceAText;
        [SerializeField] private Image choiceABar;
        [SerializeField] private TextMeshProUGUI choiceAPercentText;

        [Header("Choice B")]
        [SerializeField] private RectTransform choiceBContainer;
        [SerializeField] private TextMeshProUGUI choiceBText;
        [SerializeField] private Image choiceBBar;
        [SerializeField] private TextMeshProUGUI choiceBPercentText;

        [Header("Result Panel")]
        [SerializeField] private RectTransform resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float cardSlideDistance = 100f;
        [SerializeField] private float barAnimationSpeed = 5f;
        #endregion

        #region State
        private bool isActive = false;
        private string currentPlotId;
        private float targetChoiceAPercent = 0f;
        private float targetChoiceBPercent = 0f;
        private float currentChoiceAPercent = 0f;
        private float currentChoiceBPercent = 0f;
        private double votingEndsAt = 0;
        private Coroutine timerCoroutine;
        private bool isSubscribed = false;
        private Coroutine subscribeCoroutine;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Initialize UI state
            if (mainPanel != null) mainPanel.alpha = 0;
            if (mainPanel != null) mainPanel.blocksRaycasts = false;
            HideAllPanels();
        }

        private void OnEnable()
        {
            // Start coroutine to subscribe when NetworkManager is ready
            subscribeCoroutine = StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            // Stop subscribe coroutine if running
            if (subscribeCoroutine != null)
            {
                StopCoroutine(subscribeCoroutine);
                subscribeCoroutine = null;
            }

            // Unsubscribe from network events
            UnsubscribeFromNetwork();
        }

        private IEnumerator SubscribeWhenReady()
        {
            // Wait until NetworkManager is available
            while (NetworkManager.Instance == null)
            {
                yield return null;
            }
            SubscribeToNetwork();
        }

        private void SubscribeToNetwork()
        {
            if (isSubscribed) return;

            var network = NetworkManager.Instance;
            if (network == null) return;

            network.OnModeChange += HandleModeChange;
            network.OnNarrativePlot += HandleNarrativePlot;
            network.OnVoteStarted += HandleVoteStarted;
            network.OnVoteUpdate += HandleVoteUpdate;
            network.OnVoteResult += HandleVoteResult;
            network.OnResolutionApplied += HandleResolutionApplied;
            isSubscribed = true;
        }

        private void UnsubscribeFromNetwork()
        {
            if (!isSubscribed) return;

            var network = NetworkManager.Instance;
            if (network == null)
            {
                isSubscribed = false;
                return;
            }

            network.OnModeChange -= HandleModeChange;
            network.OnNarrativePlot -= HandleNarrativePlot;
            network.OnVoteStarted -= HandleVoteStarted;
            network.OnVoteUpdate -= HandleVoteUpdate;
            network.OnVoteResult -= HandleVoteResult;
            network.OnResolutionApplied -= HandleResolutionApplied;
            isSubscribed = false;
        }

        private void Update()
        {
            // Smoothly animate voting bars
            if (isActive && votingPanel != null && votingPanel.gameObject.activeSelf)
            {
                AnimateVotingBars();
            }
        }
        #endregion

        #region Event Handlers
        private void HandleModeChange(ModeChangeData data)
        {
            Debug.Log($"[NarrativeUI] Mode changed: {data.old_mode} -> {data.mode}");

            switch (data.mode)
            {
                case "simulation":
                    // Fade out UI when returning to simulation
                    if (isActive)
                    {
                        StartCoroutine(FadeOutUI());
                    }
                    break;

                case "narrative":
                case "voting":
                    // Ensure UI is visible
                    if (!isActive)
                    {
                        StartCoroutine(FadeInUI());
                    }
                    break;

                case "resolution":
                    // Keep UI visible for resolution display
                    break;
            }
        }

        private void HandleNarrativePlot(NarrativePlotData data)
        {
            Debug.Log($"[NarrativeUI] Narrative plot: {data.title}");
            currentPlotId = data.plot_id;

            // Show event card
            ShowEventCard(data.title, data.description);

            // Prepare voting choices
            if (data.choices != null && data.choices.Count >= 2)
            {
                SetupVotingChoices(data.choices[0].text, data.choices[1].text);
            }
        }

        private void HandleVoteStarted(VoteStartedData data)
        {
            Debug.Log($"[NarrativeUI] Vote started: {data.vote_id}");

            votingEndsAt = data.ends_at;

            // Setup choices if not already done
            if (data.choices != null && data.choices.Count >= 2)
            {
                SetupVotingChoices(data.choices[0].text, data.choices[1].text);
            }

            // Show voting panel
            ShowVotingPanel();

            // Start countdown timer
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            timerCoroutine = StartCoroutine(UpdateTimer());
        }

        private void HandleVoteUpdate(VoteUpdateData data)
        {
            // Update target percentages for smooth animation
            if (data.percentages != null && data.percentages.Count >= 2)
            {
                targetChoiceAPercent = data.percentages[0];
                targetChoiceBPercent = data.percentages[1];
            }

            // Sync timer with server's remaining_seconds to avoid clock drift
            if (data.remaining_seconds > 0)
            {
                double now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                votingEndsAt = now + data.remaining_seconds;
            }

            // Update total votes display
            if (totalVotesText != null)
            {
                totalVotesText.text = $"{data.total_votes} votes";
            }
        }

        private void HandleVoteResult(VoteResultData data)
        {
            Debug.Log($"[NarrativeUI] Vote result: {data.winning_choice_text}");

            // Stop timer
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }

            // Flash winning choice
            StartCoroutine(FlashWinningChoice(data.winning_index));

            // Show result briefly
            ShowResult($"The Audience Has Spoken!", data.winning_choice_text);
        }

        private void HandleResolutionApplied(ResolutionAppliedData data)
        {
            Debug.Log($"[NarrativeUI] Resolution: {data.message}");

            // Update result display with full resolution message
            ShowResult("Consequence", data.message);

            // Auto-hide after delay
            StartCoroutine(HideAfterDelay(5f));
        }
        #endregion

        #region UI Control Methods
        private void HideAllPanels()
        {
            if (eventCard != null) eventCard.gameObject.SetActive(false);
            if (votingPanel != null) votingPanel.gameObject.SetActive(false);
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
        }

        private void ShowEventCard(string title, string description)
        {
            if (eventCard == null) return;

            // Set content
            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;

            // Show card with animation
            eventCard.gameObject.SetActive(true);
            StartCoroutine(SlideInCard(eventCard));
        }

        private void SetupVotingChoices(string choiceA, string choiceB)
        {
            if (choiceAText != null) choiceAText.text = $"!1  {choiceA}";
            if (choiceBText != null) choiceBText.text = $"!2  {choiceB}";

            // Reset percentages
            targetChoiceAPercent = 50f;
            targetChoiceBPercent = 50f;
            currentChoiceAPercent = 50f;
            currentChoiceBPercent = 50f;

            UpdateVotingBarsImmediate();
        }

        private void ShowVotingPanel()
        {
            if (votingPanel == null) return;

            votingPanel.gameObject.SetActive(true);
            StartCoroutine(SlideInCard(votingPanel));
        }

        private void ShowResult(string title, string message)
        {
            if (resultPanel == null) return;

            // Hide other panels
            if (eventCard != null) eventCard.gameObject.SetActive(false);
            if (votingPanel != null) votingPanel.gameObject.SetActive(false);

            // Set content
            if (resultTitleText != null) resultTitleText.text = title;
            if (resultMessageText != null) resultMessageText.text = message;

            // Show result
            resultPanel.gameObject.SetActive(true);
            StartCoroutine(SlideInCard(resultPanel));
        }

        private void AnimateVotingBars()
        {
            // Smoothly interpolate bar widths
            currentChoiceAPercent = Mathf.Lerp(
                currentChoiceAPercent,
                targetChoiceAPercent,
                Time.deltaTime * barAnimationSpeed
            );
            currentChoiceBPercent = Mathf.Lerp(
                currentChoiceBPercent,
                targetChoiceBPercent,
                Time.deltaTime * barAnimationSpeed
            );

            UpdateVotingBarsImmediate();
        }

        private void UpdateVotingBarsImmediate()
        {
            // Update bar fill amounts (assuming horizontal fill)
            if (choiceABar != null)
            {
                choiceABar.fillAmount = currentChoiceAPercent / 100f;
            }
            if (choiceBBar != null)
            {
                choiceBBar.fillAmount = currentChoiceBPercent / 100f;
            }

            // Update percentage texts
            if (choiceAPercentText != null)
            {
                choiceAPercentText.text = $"{Mathf.RoundToInt(currentChoiceAPercent)}%";
            }
            if (choiceBPercentText != null)
            {
                choiceBPercentText.text = $"{Mathf.RoundToInt(currentChoiceBPercent)}%";
            }
        }
        #endregion

        #region Coroutines
        private IEnumerator FadeInUI()
        {
            isActive = true;
            if (mainPanel == null) yield break;

            mainPanel.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                mainPanel.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            mainPanel.alpha = 1f;

            // Darken background
            if (backgroundOverlay != null)
            {
                Color c = backgroundOverlay.color;
                c.a = 0.6f;
                backgroundOverlay.color = c;
            }
        }

        private IEnumerator FadeOutUI()
        {
            if (mainPanel == null)
            {
                isActive = false;
                yield break;
            }

            float elapsed = 0f;
            float startAlpha = mainPanel.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                mainPanel.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            mainPanel.alpha = 0f;
            mainPanel.blocksRaycasts = false;
            isActive = false;

            HideAllPanels();
        }

        private IEnumerator SlideInCard(RectTransform card)
        {
            if (card == null) yield break;

            Vector2 startPos = card.anchoredPosition;
            Vector2 targetPos = startPos;
            startPos.y -= cardSlideDistance;

            card.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                // Ease out cubic
                t = 1f - Mathf.Pow(1f - t, 3f);
                card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            card.anchoredPosition = targetPos;
        }

        private IEnumerator UpdateTimer()
        {
            while (votingEndsAt > 0)
            {
                double now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double remaining = votingEndsAt - now;

                if (remaining <= 0)
                {
                    if (timerText != null) timerText.text = "0s";
                    break;
                }

                if (timerText != null)
                {
                    timerText.text = $"{Mathf.CeilToInt((float)remaining)}s";
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator FlashWinningChoice(int winningIndex)
        {
            // Flash the winning choice bar
            Image winningBar = winningIndex == 0 ? choiceABar : choiceBBar;
            if (winningBar == null) yield break;

            Color originalColor = winningBar.color;
            Color flashColor = Color.yellow;

            for (int i = 0; i < 3; i++)
            {
                winningBar.color = flashColor;
                yield return new WaitForSeconds(0.15f);
                winningBar.color = originalColor;
                yield return new WaitForSeconds(0.15f);
            }
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // The mode will change to simulation, which will trigger fade out
            // But we can also force it here as a fallback
            if (isActive)
            {
                StartCoroutine(FadeOutUI());
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Force show the narrative UI (for testing).
        /// </summary>
        public void ForceShow()
        {
            StartCoroutine(FadeInUI());
        }

        /// <summary>
        /// Force hide the narrative UI.
        /// </summary>
        public void ForceHide()
        {
            StartCoroutine(FadeOutUI());
        }

        /// <summary>
        /// Test the UI with sample data.
        /// </summary>
        [ContextMenu("Test Narrative UI")]
        public void TestUI()
        {
            // Create test data
            var plotData = new NarrativePlotData
            {
                plot_id = "test_001",
                title = "Mysterious Footprints",
                description = "Strange footprints appear on the beach. Someone has been watching...",
                choices = new List<PlotChoiceData>
                {
                    new PlotChoiceData { choice_id = "investigate", text = "Follow the tracks" },
                    new PlotChoiceData { choice_id = "fortify", text = "Strengthen defenses" }
                },
                ttl_seconds = 60
            };

            // Simulate events
            HandleModeChange(new ModeChangeData { mode = "narrative", old_mode = "simulation", message = "Director intervenes..." });
            HandleNarrativePlot(plotData);

            // Simulate vote start after delay
            StartCoroutine(SimulateVoting());
        }

        private IEnumerator SimulateVoting()
        {
            yield return new WaitForSeconds(2f);

            HandleVoteStarted(new VoteStartedData
            {
                vote_id = "test_vote",
                duration_seconds = 30,
                ends_at = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30,
                choices = new List<PlotChoiceData>
                {
                    new PlotChoiceData { choice_id = "investigate", text = "Follow the tracks" },
                    new PlotChoiceData { choice_id = "fortify", text = "Strengthen defenses" }
                }
            });

            // Simulate vote updates
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(1f);
                HandleVoteUpdate(new VoteUpdateData
                {
                    vote_id = "test_vote",
                    tallies = new List<int> { Random.Range(10, 50), Random.Range(10, 50) },
                    percentages = new List<float> { Random.Range(30f, 70f), Random.Range(30f, 70f) },
                    total_votes = Random.Range(20, 100),
                    remaining_seconds = 30 - i
                });
            }
        }
        #endregion
    }
}
