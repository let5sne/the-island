using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Models;
using TheIsland.Network;
using TheIsland.Agents;
using TheIsland.UI;
using TheIsland.Visual;

namespace TheIsland.Core
{
    /// <summary>
    /// Main game controller.
    /// Manages agent spawning, UI updates, and event handling.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        private static GameManager _instance;
        public static GameManager Instance => _instance;
        #endregion

        #region Configuration
        [Header("Agent Spawning")]
        [SerializeField] private GameObject agentPrefab;
        [SerializeField] private Transform agentContainer;
        [SerializeField] private Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(-3f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(3f, 0f, 0f)
        };

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI connectionStatus;
        [SerializeField] private TextMeshProUGUI tickInfo;
        [SerializeField] private TextMeshProUGUI goldDisplay;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_InputField commandInput;
        [SerializeField] private Button sendButton;

        [Header("Notification Panel")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TextMeshProUGUI notificationText;
        [SerializeField] private float notificationDuration = 3f;
        #endregion

        #region Private Fields
        private Dictionary<int, AgentController> _agents = new Dictionary<int, AgentController>();
        private Dictionary<int, AgentUI> _agentUIs = new Dictionary<int, AgentUI>();
        private Dictionary<int, AgentVisual> _agentVisuals = new Dictionary<int, AgentVisual>();
        private int _playerGold = 100;
        private int _currentTick;
        private int _currentDay;
        private int _nextSpawnIndex;

        // World state
        private string _currentTimeOfDay = "day";
        private string _currentWeather = "Sunny";
        #endregion

        #region Properties
        public int PlayerGold => _playerGold;
        public string CurrentTimeOfDay => _currentTimeOfDay;
        public string CurrentWeather => _currentWeather;
        public int CurrentDay => _currentDay;
        public int AliveAgentCount
        {
            get
            {
                int count = 0;
                // Check AgentVisual first (newest system)
                foreach (var visual in _agentVisuals.Values)
                {
                    if (visual.IsAlive) count++;
                }
                if (count > 0) return count;

                // Fallback to AgentUI
                foreach (var agentUI in _agentUIs.Values)
                {
                    if (agentUI.IsAlive) count++;
                }
                if (count > 0) return count;

                // Fallback to AgentController (legacy)
                foreach (var agent in _agents.Values)
                {
                    if (agent.IsAlive) count++;
                }
                return count;
            }
        }
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
        }

        private void Start()
        {
            // Subscribe to network events
            SubscribeToNetworkEvents();

            // Setup UI
            SetupUI();

            // Initial connection status
            UpdateConnectionStatus(false);
        }

        private void OnDestroy()
        {
            // Unsubscribe from network events
            UnsubscribeFromNetworkEvents();

            // Cleanup UI listeners
            CleanupUI();
        }
        #endregion

        #region Network Event Subscription
        private void SubscribeToNetworkEvents()
        {
            var network = NetworkManager.Instance;
            if (network == null) return;

            network.OnConnected += HandleConnected;
            network.OnDisconnected += HandleDisconnected;
            network.OnAgentsUpdate += HandleAgentsUpdate;
            network.OnAgentSpeak += HandleAgentSpeak;
            network.OnAgentDied += HandleAgentDied;
            network.OnFeed += HandleFeed;
            network.OnTick += HandleTick;
            network.OnSystemMessage += HandleSystemMessage;
            network.OnUserUpdate += HandleUserUpdate;

            // New phase events
            network.OnWeatherChange += HandleWeatherChange;
            network.OnPhaseChange += HandlePhaseChange;
            network.OnDayChange += HandleDayChange;
            network.OnHeal += HandleHeal;
            network.OnEncourage += HandleEncourage;
            network.OnTalk += HandleTalk;
            network.OnRevive += HandleRevive;
            network.OnSocialInteraction += HandleSocialInteraction;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var network = NetworkManager.Instance;
            if (network == null) return;

            network.OnConnected -= HandleConnected;
            network.OnDisconnected -= HandleDisconnected;
            network.OnAgentsUpdate -= HandleAgentsUpdate;
            network.OnAgentSpeak -= HandleAgentSpeak;
            network.OnAgentDied -= HandleAgentDied;
            network.OnFeed -= HandleFeed;
            network.OnTick -= HandleTick;
            network.OnSystemMessage -= HandleSystemMessage;
            network.OnUserUpdate -= HandleUserUpdate;

            // New phase events
            network.OnWeatherChange -= HandleWeatherChange;
            network.OnPhaseChange -= HandlePhaseChange;
            network.OnDayChange -= HandleDayChange;
            network.OnHeal -= HandleHeal;
            network.OnEncourage -= HandleEncourage;
            network.OnTalk -= HandleTalk;
            network.OnRevive -= HandleRevive;
            network.OnSocialInteraction -= HandleSocialInteraction;
        }
        #endregion

        #region UI Setup
        private void SetupUI()
        {
            // Reset button
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClicked);
            }

            // Send button
            if (sendButton != null)
            {
                sendButton.onClick.RemoveAllListeners();
                sendButton.onClick.AddListener(OnSendClicked);
            }

            // Command input enter key
            if (commandInput != null)
            {
                commandInput.onSubmit.RemoveAllListeners();
                commandInput.onSubmit.AddListener(OnCommandSubmit);
            }

            // Hide notification initially
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }

            UpdateGoldDisplay();
        }

        private void CleanupUI()
        {
            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetClicked);
            }
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(OnSendClicked);
            }
            if (commandInput != null)
            {
                commandInput.onSubmit.RemoveListener(OnCommandSubmit);
            }
        }

        private void UpdateConnectionStatus(bool connected)
        {
            if (connectionStatus == null) return;

            connectionStatus.text = connected ? "Connected" : "Disconnected";
            connectionStatus.color = connected ? Color.green : Color.red;
        }

        private void UpdateTickInfo()
        {
            if (tickInfo == null) return;

            // Format time of day nicely
            string timeDisplay = _currentTimeOfDay switch
            {
                "dawn" => "Dawn",
                "day" => "Day",
                "dusk" => "Dusk",
                "night" => "Night",
                _ => "Day"
            };

            tickInfo.text = $"Day {_currentDay} | {timeDisplay} | {_currentWeather} | Tick {_currentTick} | Alive: {AliveAgentCount}";
        }

        private void UpdateGoldDisplay()
        {
            if (goldDisplay == null) return;

            goldDisplay.text = $"Gold: {_playerGold}";
        }
        #endregion

        #region Network Event Handlers
        private void HandleConnected()
        {
            Debug.Log("[GameManager] Connected to server");
            UpdateConnectionStatus(true);
            ShowNotification("Connected to The Island!");
        }

        private void HandleDisconnected()
        {
            Debug.Log("[GameManager] Disconnected from server");
            UpdateConnectionStatus(false);
            ShowNotification("Disconnected from server", isError: true);
        }

        private void HandleAgentsUpdate(List<AgentData> agentsData)
        {
            // Null check to prevent exceptions
            if (agentsData == null || agentsData.Count == 0)
            {
                Debug.LogWarning("[GameManager] Received empty agents update");
                return;
            }

            foreach (var data in agentsData)
            {
                // Check for AgentVisual first (newest system - 2.5D sprites)
                if (_agentVisuals.TryGetValue(data.id, out AgentVisual agentVisual))
                {
                    agentVisual.UpdateStats(data);
                }
                // Check for AgentUI (programmatic UI system)
                else if (_agentUIs.TryGetValue(data.id, out AgentUI agentUI))
                {
                    agentUI.UpdateStats(data);
                }
                // Fallback to AgentController (legacy)
                else if (_agents.TryGetValue(data.id, out AgentController controller))
                {
                    controller.UpdateStats(data);
                }
                else
                {
                    // Spawn new agent
                    SpawnAgent(data);
                }
            }

            UpdateTickInfo();
        }

        private void HandleAgentSpeak(AgentSpeakData data)
        {
            // Check AgentVisual first (newest system - 2.5D sprites)
            if (_agentVisuals.TryGetValue(data.agent_id, out AgentVisual agentVisual))
            {
                agentVisual.ShowSpeech(data.text);
            }
            // Check AgentUI (programmatic UI system)
            else if (_agentUIs.TryGetValue(data.agent_id, out AgentUI agentUI))
            {
                agentUI.ShowSpeech(data.text);
            }
            // Fallback to AgentController (legacy)
            else if (_agents.TryGetValue(data.agent_id, out AgentController controller))
            {
                controller.ShowSpeech(data.text);
            }
            else
            {
                Debug.LogWarning($"[GameManager] Agent {data.agent_id} not found for speech");
            }
        }

        private void HandleAgentDied(AgentDiedData data)
        {
            Debug.Log($"[GameManager] Agent died: {data.agent_name}");
            ShowNotification(data.message, isError: true);
        }

        private void HandleFeed(FeedEventData data)
        {
            Debug.Log($"[GameManager] Feed event: {data.message}");

            // Update gold if this was our action
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.user_gold;
                UpdateGoldDisplay();
            }

            ShowNotification(data.message);
        }

        private void HandleTick(TickData data)
        {
            _currentTick = data.tick;
            _currentDay = data.day;

            // Update weather and time of day from tick data
            if (!string.IsNullOrEmpty(data.time_of_day))
            {
                _currentTimeOfDay = data.time_of_day;
            }
            if (!string.IsNullOrEmpty(data.weather))
            {
                _currentWeather = data.weather;
            }

            UpdateTickInfo();
        }

        private void HandleSystemMessage(SystemEventData data)
        {
            Debug.Log($"[GameManager] System: {data.message}");
            ShowNotification(data.message);
        }

        private void HandleUserUpdate(UserUpdateData data)
        {
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.gold;
                UpdateGoldDisplay();
            }
        }

        private void HandleWeatherChange(WeatherChangeData data)
        {
            Debug.Log($"[GameManager] Weather changed: {data.old_weather} -> {data.new_weather}");
            _currentWeather = data.new_weather;
            ShowNotification($"Weather: {data.new_weather}");
            UpdateTickInfo();
        }

        private void HandlePhaseChange(PhaseChangeData data)
        {
            Debug.Log($"[GameManager] Phase changed: {data.old_phase} -> {data.new_phase}");
            _currentTimeOfDay = data.new_phase;
            ShowNotification($"The {data.new_phase} begins...");
            UpdateTickInfo();
        }

        private void HandleDayChange(DayChangeData data)
        {
            Debug.Log($"[GameManager] New day: {data.day}");
            _currentDay = data.day;
            ShowNotification($"Day {data.day} begins!");
            UpdateTickInfo();
        }

        private void HandleHeal(HealEventData data)
        {
            Debug.Log($"[GameManager] Heal event: {data.message}");

            // Update gold if this was our action
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.user_gold;
                UpdateGoldDisplay();
            }

            ShowNotification(data.message);
        }

        private void HandleEncourage(EncourageEventData data)
        {
            Debug.Log($"[GameManager] Encourage event: {data.message}");

            // Update gold if this was our action
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.user_gold;
                UpdateGoldDisplay();
            }

            ShowNotification(data.message);
        }

        private void HandleTalk(TalkEventData data)
        {
            Debug.Log($"[GameManager] Talk event: {data.agent_name} responds about '{data.topic}'");

            // Show the agent's speech response
            if (_agentVisuals.TryGetValue(GetAgentIdByName(data.agent_name), out AgentVisual agentVisual))
            {
                agentVisual.ShowSpeech(data.response);
            }
            else if (_agentUIs.TryGetValue(GetAgentIdByName(data.agent_name), out AgentUI agentUI))
            {
                agentUI.ShowSpeech(data.response);
            }
        }

        private void HandleRevive(ReviveEventData data)
        {
            Debug.Log($"[GameManager] Revive event: {data.message}");

            // Update gold if this was our action
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.user_gold;
                UpdateGoldDisplay();
            }

            ShowNotification(data.message);
        }

        private void HandleSocialInteraction(SocialInteractionData data)
        {
            Debug.Log($"[GameManager] Social: {data.initiator_name} -> {data.target_name} ({data.interaction_type})");

            // Show dialogue from initiator
            if (_agentVisuals.TryGetValue(data.initiator_id, out AgentVisual initiatorVisual))
            {
                initiatorVisual.ShowSpeech(data.dialogue);
            }
            else if (_agentUIs.TryGetValue(data.initiator_id, out AgentUI initiatorUI))
            {
                initiatorUI.ShowSpeech(data.dialogue);
            }
        }
        #endregion

        #region Agent Management
        private void SpawnAgent(AgentData data)
        {
            if (agentPrefab == null)
            {
                Debug.LogError("[GameManager] Agent prefab not assigned!");
                return;
            }

            // Determine spawn position
            Vector3 spawnPos = GetNextSpawnPosition();

            // Instantiate prefab
            GameObject agentObj = Instantiate(
                agentPrefab,
                spawnPos,
                Quaternion.identity,
                agentContainer
            );

            // Try to get AgentVisual first (newest system - 2.5D sprites)
            AgentVisual agentVisual = agentObj.GetComponent<AgentVisual>();
            if (agentVisual != null)
            {
                agentVisual.Initialize(data);
                _agentVisuals[data.id] = agentVisual;
                Debug.Log($"[GameManager] Spawned agent (AgentVisual): {data.name} at {spawnPos}");
                return;
            }

            // Try to get AgentUI (programmatic UI system)
            AgentUI agentUI = agentObj.GetComponent<AgentUI>();
            if (agentUI == null)
            {
                // Add AgentUI component - it will create all UI elements automatically
                agentUI = agentObj.AddComponent<AgentUI>();
            }
            agentUI.Initialize(data);
            _agentUIs[data.id] = agentUI;

            // Also check for legacy AgentController
            AgentController controller = agentObj.GetComponent<AgentController>();
            if (controller != null)
            {
                controller.Initialize(data);
                _agents[data.id] = controller;
            }

            Debug.Log($"[GameManager] Spawned agent: {data.name} at {spawnPos}");
        }

        private Vector3 GetNextSpawnPosition()
        {
            if (spawnPositions == null || spawnPositions.Length == 0)
            {
                return Vector3.zero;
            }

            Vector3 pos = spawnPositions[_nextSpawnIndex % spawnPositions.Length];
            _nextSpawnIndex++;
            return pos;
        }

        /// <summary>
        /// Get an agent controller by ID.
        /// </summary>
        public AgentController GetAgent(int agentId)
        {
            _agents.TryGetValue(agentId, out AgentController controller);
            return controller;
        }

        /// <summary>
        /// Get an agent controller by name.
        /// </summary>
        public AgentController GetAgentByName(string name)
        {
            foreach (var agent in _agents.Values)
            {
                if (agent.CurrentData?.name == name)
                {
                    return agent;
                }
            }
            return null;
        }

        /// <summary>
        /// Get agent ID by name (searches all agent systems).
        /// </summary>
        private int GetAgentIdByName(string name)
        {
            // Check AgentVisual first (newest system)
            foreach (var kvp in _agentVisuals)
            {
                if (kvp.Value.CurrentData?.name == name)
                {
                    return kvp.Key;
                }
            }

            // Check AgentUI
            foreach (var kvp in _agentUIs)
            {
                if (kvp.Value.CurrentData?.name == name)
                {
                    return kvp.Key;
                }
            }

            // Check AgentController (legacy)
            foreach (var kvp in _agents)
            {
                if (kvp.Value.CurrentData?.name == name)
                {
                    return kvp.Key;
                }
            }

            return -1;
        }
        #endregion

        #region UI Actions
        private void OnResetClicked()
        {
            NetworkManager.Instance.ResetGame();
            ShowNotification("Reset requested...");
        }

        private void OnSendClicked()
        {
            SendCommand();
        }

        private void OnCommandSubmit(string text)
        {
            SendCommand();
        }

        private void SendCommand()
        {
            if (commandInput == null || string.IsNullOrWhiteSpace(commandInput.text))
                return;

            NetworkManager.Instance.SendCommand(commandInput.text);
            commandInput.text = "";
            commandInput.ActivateInputField();
        }

        /// <summary>
        /// Feed a specific agent by name.
        /// Called from UI buttons.
        /// </summary>
        public void FeedAgent(string agentName)
        {
            NetworkManager.Instance.FeedAgent(agentName);
        }
        #endregion

        #region Notifications
        private void ShowNotification(string message, bool isError = false)
        {
            if (notificationPanel == null || notificationText == null)
                return;

            notificationText.text = message;
            notificationText.color = isError ? Color.red : Color.white;
            notificationPanel.SetActive(true);

            // Auto-hide
            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), notificationDuration);
        }

        private void HideNotification()
        {
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }
        #endregion
    }
}
