using System;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using TheIsland.Models;

namespace TheIsland.Network
{
    /// <summary>
    /// Singleton WebSocket manager for server communication.
    /// Handles connection, message parsing, and event dispatching.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        #region Singleton
        private static NetworkManager _instance;
        public static NetworkManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<NetworkManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("NetworkManager");
                        _instance = go.AddComponent<NetworkManager>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Configuration
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/ws";
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private float reconnectDelay = 3f;

        [Header("User Settings")]
        [SerializeField] private string username = "UnityPlayer";
        #endregion

        #region Events
        // Connection events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        // Game events
        public event Action<List<AgentData>> OnAgentsUpdate;
        public event Action<AgentSpeakData> OnAgentSpeak;
        public event Action<AgentDiedData> OnAgentDied;
        public event Action<FeedEventData> OnFeed;
        public event Action<TickData> OnTick;
        public event Action<SystemEventData> OnSystemMessage;
        public event Action<UserUpdateData> OnUserUpdate;

        // New Phase events
        public event Action<WeatherChangeData> OnWeatherChange;
        public event Action<PhaseChangeData> OnPhaseChange;
        public event Action<DayChangeData> OnDayChange;
        public event Action<HealEventData> OnHeal;
        public event Action<EncourageEventData> OnEncourage;
        public event Action<TalkEventData> OnTalk;
        public event Action<ReviveEventData> OnRevive;
        public event Action<SocialInteractionData> OnSocialInteraction;
        public event Action<WorldStateData> OnWorldUpdate;
        public event Action<GiftEffectData> OnGiftEffect;  // Phase 8: Gift/Donation effects
        public event Action<AgentActionData> OnAgentAction; // Phase 13: Autonomous Actions
        public event Action<CraftEventData> OnCraft;        // Phase 16: Crafting
        public event Action<UseItemEventData> OnUseItem;    // Phase 16: Using items
        public event Action<RandomEventData> OnRandomEvent; // Phase 17-C: Random Events
        #endregion

        #region Private Fields
        private WebSocket _websocket;
        private bool _isConnecting;
        private bool _shouldReconnect = true;
        private bool _hasNotifiedConnected = false;
        #endregion

        #region Properties
        public bool IsConnected => _websocket?.State == WebSocketState.Open;
        public string Username
        {
            get => username;
            set => username = value;
        }
        public string ServerUrl
        {
            get => serverUrl;
            set => serverUrl = value;
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (autoConnect)
            {
                await Connect();
            }
        }

        private void Update()
        {
            // CRITICAL: Dispatch message queue on main thread
            // NativeWebSocket requires this for callbacks to work
            #if !UNITY_WEBGL || UNITY_EDITOR
            if (_websocket != null)
            {
                _websocket.DispatchMessageQueue();

                // Fallback: Check connection state directly
                if (_websocket.State == WebSocketState.Open && !_hasNotifiedConnected)
                {
                    _hasNotifiedConnected = true;
                    Debug.Log("[NetworkManager] Connection detected via state check!");
                    OnConnected?.Invoke();
                }
                else if (_websocket.State != WebSocketState.Open && _hasNotifiedConnected)
                {
                    _hasNotifiedConnected = false;
                }
            }
            #endif
        }

        private async void OnApplicationQuit()
        {
            _shouldReconnect = false;
            if (_websocket != null)
            {
                await _websocket.Close();
            }
        }

        private void OnDestroy()
        {
            _shouldReconnect = false;
            _websocket?.Close();
        }
        #endregion

        #region Connection Management
        public async System.Threading.Tasks.Task Connect()
        {
            if (_isConnecting || IsConnected) return;

            _isConnecting = true;
            Debug.Log($"[NetworkManager] Connecting to {serverUrl}...");

            try
            {
                _websocket = new WebSocket(serverUrl);

                _websocket.OnOpen += HandleOpen;
                _websocket.OnClose += HandleClose;
                _websocket.OnError += HandleError;
                _websocket.OnMessage += HandleMessage;

                await _websocket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Connection failed: {e.Message}");
                _isConnecting = false;
                OnError?.Invoke(e.Message);

                if (_shouldReconnect)
                {
                    ScheduleReconnect();
                }
            }
        }

        public async void Disconnect()
        {
            _shouldReconnect = false;
            if (_websocket != null)
            {
                await _websocket.Close();
            }
        }

        private void ScheduleReconnect()
        {
            if (_shouldReconnect)
            {
                Debug.Log($"[NetworkManager] Reconnecting in {reconnectDelay}s...");
                Invoke(nameof(TryReconnect), reconnectDelay);
            }
        }

        private async void TryReconnect()
        {
            await Connect();
        }
        #endregion

        #region WebSocket Event Handlers
        private void HandleOpen()
        {
            _isConnecting = false;
            Debug.Log("[NetworkManager] Connected to server!");
            OnConnected?.Invoke();
        }

        private void HandleClose(WebSocketCloseCode code)
        {
            _isConnecting = false;
            Debug.Log($"[NetworkManager] Disconnected (code: {code})");
            OnDisconnected?.Invoke();

            if (_shouldReconnect && code != WebSocketCloseCode.Normal)
            {
                ScheduleReconnect();
            }
        }

        private void HandleError(string error)
        {
            _isConnecting = false;
            Debug.LogError($"[NetworkManager] WebSocket error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleMessage(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            ProcessMessage(json);
        }
        #endregion

        #region Message Processing
        private void ProcessMessage(string json)
        {
            try
            {
                // First, extract the event_type
                var baseMessage = JsonUtility.FromJson<ServerMessage>(json);

                if (string.IsNullOrEmpty(baseMessage.event_type))
                {
                    Debug.LogWarning("[NetworkManager] Received message without event_type");
                    return;
                }

                // Extract the data portion using regex (JsonUtility limitation workaround)
                string dataJson = ExtractDataJson(json);

                // Dispatch based on event type
                switch (baseMessage.event_type)
                {
                    case EventTypes.AGENTS_UPDATE:
                        var agentsData = JsonUtility.FromJson<AgentsUpdateData>(dataJson);
                        OnAgentsUpdate?.Invoke(agentsData.agents);
                        break;

                    case EventTypes.AGENT_SPEAK:
                        var speakData = JsonUtility.FromJson<AgentSpeakData>(dataJson);
                        OnAgentSpeak?.Invoke(speakData);
                        break;

                    case EventTypes.AGENT_DIED:
                        var diedData = JsonUtility.FromJson<AgentDiedData>(dataJson);
                        OnAgentDied?.Invoke(diedData);
                        break;

                    case EventTypes.FEED:
                        var feedData = JsonUtility.FromJson<FeedEventData>(dataJson);
                        OnFeed?.Invoke(feedData);
                        break;

                    case EventTypes.TICK:
                        var tickData = JsonUtility.FromJson<TickData>(dataJson);
                        OnTick?.Invoke(tickData);
                        break;

                    case EventTypes.SYSTEM:
                    case EventTypes.ERROR:
                        var sysData = JsonUtility.FromJson<SystemEventData>(dataJson);
                        OnSystemMessage?.Invoke(sysData);
                        break;

                    case EventTypes.USER_UPDATE:
                        var userData = JsonUtility.FromJson<UserUpdateData>(dataJson);
                        OnUserUpdate?.Invoke(userData);
                        break;

                    case EventTypes.WORLD_UPDATE:
                        var worldData = JsonUtility.FromJson<WorldStateData>(dataJson);
                        OnWorldUpdate?.Invoke(worldData);
                        break;

                    case EventTypes.WEATHER_CHANGE:
                        var weatherData = JsonUtility.FromJson<WeatherChangeData>(dataJson);
                        OnWeatherChange?.Invoke(weatherData);
                        break;

                    case EventTypes.PHASE_CHANGE:
                        var phaseData = JsonUtility.FromJson<PhaseChangeData>(dataJson);
                        OnPhaseChange?.Invoke(phaseData);
                        break;

                    case EventTypes.DAY_CHANGE:
                        var dayData = JsonUtility.FromJson<DayChangeData>(dataJson);
                        OnDayChange?.Invoke(dayData);
                        break;

                    case EventTypes.HEAL:
                        var healData = JsonUtility.FromJson<HealEventData>(dataJson);
                        OnHeal?.Invoke(healData);
                        break;

                    case EventTypes.ENCOURAGE:
                        var encourageData = JsonUtility.FromJson<EncourageEventData>(dataJson);
                        OnEncourage?.Invoke(encourageData);
                        break;

                    case EventTypes.TALK:
                        var talkData = JsonUtility.FromJson<TalkEventData>(dataJson);
                        OnTalk?.Invoke(talkData);
                        break;

                    case EventTypes.REVIVE:
                    case EventTypes.AUTO_REVIVE:
                        var reviveData = JsonUtility.FromJson<ReviveEventData>(dataJson);
                        OnRevive?.Invoke(reviveData);
                        break;

                    case EventTypes.SOCIAL_INTERACTION:
                        var socialData = JsonUtility.FromJson<SocialInteractionData>(dataJson);
                        OnSocialInteraction?.Invoke(socialData);
                        break;

                    case EventTypes.GIFT_EFFECT:
                        var giftData = JsonUtility.FromJson<GiftEffectData>(dataJson);
                        OnGiftEffect?.Invoke(giftData);
                        break;

                    case EventTypes.AGENT_ACTION:
                        var actionData = JsonUtility.FromJson<AgentActionData>(dataJson);
                        OnAgentAction?.Invoke(actionData);
                        break;

                    case EventTypes.CRAFT:
                        var craftData = JsonUtility.FromJson<CraftEventData>(dataJson);
                        OnCraft?.Invoke(craftData);
                        break;

                    case EventTypes.USE_ITEM:
                        var useItemData = JsonUtility.FromJson<UseItemEventData>(dataJson);
                        OnUseItem?.Invoke(useItemData);
                        break;

                    case EventTypes.COMMENT:
                        // Comments can be logged but typically not displayed in 3D
                        Debug.Log($"[Chat] {json}");
                        break;

                    case EventTypes.RANDOM_EVENT:
                        var randomEventData = JsonUtility.FromJson<RandomEventData>(dataJson);
                        OnRandomEvent?.Invoke(randomEventData);
                        Debug.Log($"[Random Event] {randomEventData.event_type}: {randomEventData.message}");
                        break;

                    default:
                        Debug.Log($"[NetworkManager] Unhandled event type: {baseMessage.event_type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Failed to process message: {e.Message}\nJSON: {json}");
            }
        }

        /// <summary>
        /// Extract the "data" object as a JSON string for nested deserialization.
        /// Uses a balanced bracket approach for better reliability.
        /// </summary>
        private string ExtractDataJson(string fullJson)
        {
            // Find the start of "data":
            int dataIndex = fullJson.IndexOf("\"data\"");
            if (dataIndex == -1) return "{}";

            // Find the colon after "data"
            int colonIndex = fullJson.IndexOf(':', dataIndex);
            if (colonIndex == -1) return "{}";

            // Skip whitespace after colon
            int startIndex = colonIndex + 1;
            while (startIndex < fullJson.Length && char.IsWhiteSpace(fullJson[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= fullJson.Length) return "{}";

            char firstChar = fullJson[startIndex];

            // Handle object
            if (firstChar == '{')
            {
                return ExtractBalancedBrackets(fullJson, startIndex, '{', '}');
            }
            // Handle array
            else if (firstChar == '[')
            {
                return ExtractBalancedBrackets(fullJson, startIndex, '[', ']');
            }
            // Handle primitive (string, number, bool, null)
            else
            {
                return "{}"; // Primitives not expected for our protocol
            }
        }

        /// <summary>
        /// Extract a balanced bracket section from JSON string.
        /// Handles nested brackets and escaped strings properly.
        /// </summary>
        private string ExtractBalancedBrackets(string json, int start, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == open) depth++;
                    else if (c == close)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return json.Substring(start, i - start + 1);
                        }
                    }
                }
            }

            return "{}"; // Fallback if unbalanced
        }
        #endregion

        #region Sending Messages
        public async void SendCommand(string command)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Cannot send - not connected");
                return;
            }

            var message = new ClientMessage
            {
                action = "send_comment",
                payload = new ClientPayload
                {
                    user = username,
                    message = command
                }
            };

            string json = JsonUtility.ToJson(message);
            await _websocket.SendText(json);
            Debug.Log($"[NetworkManager] Sent: {json}");
        }

        public void FeedAgent(string agentName)
        {
            SendCommand($"feed {agentName}");
        }

        public void HealAgent(string agentName)
        {
            SendCommand($"heal {agentName}");
        }

        public void EncourageAgent(string agentName)
        {
            SendCommand($"encourage {agentName}");
        }

        public void TalkToAgent(string agentName, string topic = "")
        {
            string cmd = string.IsNullOrEmpty(topic)
                ? $"talk {agentName}"
                : $"talk {agentName} {topic}";
            SendCommand(cmd);
        }

        public void ReviveAgent(string agentName)
        {
            SendCommand($"revive {agentName}");
        }

        public void CheckStatus()
        {
            SendCommand("check");
        }

        public void ResetGame()
        {
            SendCommand("reset");
        }
        #endregion
    }
}
