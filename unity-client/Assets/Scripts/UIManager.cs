using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TheIsland.Network;
using TheIsland.Models;

namespace TheIsland.UI
{
    /// <summary>
    /// Main UI Manager - Creates and manages the game's UI canvas programmatically.
    /// Attach this to an empty GameObject, it will create all UI elements automatically.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        #region Singleton
        private static UIManager _instance;
        public static UIManager Instance => _instance;
        #endregion

        #region UI References (Auto-created)
        private Canvas _canvas;
        private TextMeshProUGUI _connectionStatus;
        private TextMeshProUGUI _goldDisplay;
        private TextMeshProUGUI _tickInfo;
        private TMP_InputField _commandInput;
        private Button _sendButton;
        private Button _resetButton;
        private GameObject _notificationPanel;
        private TextMeshProUGUI _notificationText;
        #endregion

        #region State
        private int _playerGold = 100;
        private int _currentDay = 1;
        private int _currentTick = 0;
        private int _aliveCount = 3;
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

            CreateUI();
        }

        private void Start()
        {
            SubscribeToEvents();
            UpdateAllUI();

            // Check if already connected (in case we missed the event)
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                OnConnected();
            }

            // Start periodic connection check
            InvokeRepeating(nameof(CheckConnectionStatus), 1f, 1f);
        }

        private void CheckConnectionStatus()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                if (_connectionStatus != null && _connectionStatus.text.Contains("Disconnected"))
                {
                    OnConnected();
                    Debug.Log("[UIManager] Connection detected via periodic check");
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        #endregion

        #region Event Subscription
        private void SubscribeToEvents()
        {
            var network = NetworkManager.Instance;
            if (network == null)
            {
                Debug.LogError("[UIManager] NetworkManager.Instance is null!");
                return;
            }

            Debug.Log($"[UIManager] Subscribing to NetworkManager events (Instance ID: {network.GetInstanceID()})");
            network.OnConnected += OnConnected;
            network.OnDisconnected += OnDisconnected;
            network.OnTick += OnTick;
            network.OnFeed += OnFeed;
            network.OnUserUpdate += OnUserUpdate;
            network.OnAgentDied += OnAgentDied;
            network.OnSystemMessage += OnSystemMessage;
            network.OnAgentsUpdate += OnAgentsUpdate;
        }

        private void UnsubscribeFromEvents()
        {
            var network = NetworkManager.Instance;
            if (network == null) return;

            network.OnConnected -= OnConnected;
            network.OnDisconnected -= OnDisconnected;
            network.OnTick -= OnTick;
            network.OnFeed -= OnFeed;
            network.OnUserUpdate -= OnUserUpdate;
            network.OnAgentDied -= OnAgentDied;
            network.OnSystemMessage -= OnSystemMessage;
            network.OnAgentsUpdate -= OnAgentsUpdate;
        }
        #endregion

        #region UI Creation
        private void CreateUI()
        {
            // Create EventSystem if not exists (required for UI input)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                Debug.Log("[UIManager] Created EventSystem for UI input");
            }

            // Create Canvas
            var canvasObj = new GameObject("GameCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create UI Elements
            CreateTopBar();
            CreateBottomBar();
            CreateNotificationPanel();
        }

        private void CreateTopBar()
        {
            // Top bar container
            var topBar = CreatePanel("TopBar", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, -10), new Vector2(0, 60));
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.offsetMin = new Vector2(10, -70);
            topBar.offsetMax = new Vector2(-10, -10);

            var topBarImg = topBar.gameObject.AddComponent<Image>();
            topBarImg.color = new Color(0, 0, 0, 0.7f);

            // Connection Status (Left)
            _connectionStatus = CreateText(topBar, "ConnectionStatus", "● Disconnected",
                TextAlignmentOptions.Left, 24, Color.red);
            var connRect = _connectionStatus.rectTransform;
            connRect.anchorMin = new Vector2(0, 0);
            connRect.anchorMax = new Vector2(0.3f, 1);
            connRect.offsetMin = new Vector2(20, 10);
            connRect.offsetMax = new Vector2(0, -10);

            // Tick Info (Center)
            _tickInfo = CreateText(topBar, "TickInfo", "Day 1 | Tick 0 | Alive: 3",
                TextAlignmentOptions.Center, 22, Color.white);
            var tickRect = _tickInfo.rectTransform;
            tickRect.anchorMin = new Vector2(0.3f, 0);
            tickRect.anchorMax = new Vector2(0.7f, 1);
            tickRect.offsetMin = new Vector2(0, 10);
            tickRect.offsetMax = new Vector2(0, -10);

            // Gold Display (Right)
            _goldDisplay = CreateText(topBar, "GoldDisplay", "[G] 100 Gold",
                TextAlignmentOptions.Right, 28, new Color(1f, 0.84f, 0f));
            var goldRect = _goldDisplay.rectTransform;
            goldRect.anchorMin = new Vector2(0.7f, 0);
            goldRect.anchorMax = new Vector2(1, 1);
            goldRect.offsetMin = new Vector2(0, 10);
            goldRect.offsetMax = new Vector2(-20, -10);
        }

        private void CreateBottomBar()
        {
            // Bottom bar container
            var bottomBar = CreatePanel("BottomBar", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(10, 10), new Vector2(-10, 70));
            bottomBar.anchorMin = new Vector2(0, 0);
            bottomBar.anchorMax = new Vector2(1, 0);
            bottomBar.offsetMin = new Vector2(10, 10);
            bottomBar.offsetMax = new Vector2(-10, 70);

            var bottomBarImg = bottomBar.gameObject.AddComponent<Image>();
            bottomBarImg.color = new Color(0, 0, 0, 0.7f);

            // Command Input
            var inputObj = new GameObject("CommandInput");
            inputObj.transform.SetParent(bottomBar);

            // Add RectTransform first (required for UI elements)
            var inputRect = inputObj.AddComponent<RectTransform>();

            _commandInput = inputObj.AddComponent<TMP_InputField>();
            inputRect.anchorMin = new Vector2(0, 0);
            inputRect.anchorMax = new Vector2(0.6f, 1);
            inputRect.offsetMin = new Vector2(10, 10);
            inputRect.offsetMax = new Vector2(-5, -10);

            // Input background
            var inputBg = new GameObject("Background");
            inputBg.transform.SetParent(inputObj.transform);
            var inputBgImg = inputBg.AddComponent<Image>();
            inputBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var inputBgRect = inputBg.GetComponent<RectTransform>();
            inputBgRect.anchorMin = Vector2.zero;
            inputBgRect.anchorMax = Vector2.one;
            inputBgRect.offsetMin = Vector2.zero;
            inputBgRect.offsetMax = Vector2.zero;

            // Input text area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            var inputText = new GameObject("Text");
            inputText.transform.SetParent(textArea.transform);
            var inputTMP = inputText.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = 20;
            inputTMP.color = Color.white;
            var inputTextRect = inputText.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform);
            var placeholderTMP = placeholder.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = "Enter command (feed Jack, check, reset)...";
            placeholderTMP.fontSize = 20;
            placeholderTMP.fontStyle = FontStyles.Italic;
            placeholderTMP.color = new Color(0.5f, 0.5f, 0.5f);
            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            _commandInput.textViewport = textAreaRect;
            _commandInput.textComponent = inputTMP;
            _commandInput.placeholder = placeholderTMP;
            _commandInput.onSubmit.AddListener(OnCommandSubmit);

            // Send Button
            _sendButton = CreateButton(bottomBar, "SendButton", "Send", new Color(0.3f, 0.7f, 0.3f), OnSendClicked);
            var sendRect = _sendButton.GetComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(0.6f, 0);
            sendRect.anchorMax = new Vector2(0.78f, 1);
            sendRect.offsetMin = new Vector2(5, 10);
            sendRect.offsetMax = new Vector2(-5, -10);

            // Reset Button
            _resetButton = CreateButton(bottomBar, "ResetButton", "Reset", new Color(0.8f, 0.3f, 0.3f), OnResetClicked);
            var resetRect = _resetButton.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(0.78f, 0);
            resetRect.anchorMax = new Vector2(1, 1);
            resetRect.offsetMin = new Vector2(5, 10);
            resetRect.offsetMax = new Vector2(-10, -10);
        }

        private void CreateNotificationPanel()
        {
            _notificationPanel = new GameObject("NotificationPanel");
            _notificationPanel.transform.SetParent(_canvas.transform);

            var panelRect = _notificationPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.8f);
            panelRect.anchorMax = new Vector2(0.5f, 0.8f);
            panelRect.sizeDelta = new Vector2(600, 60);

            var panelImg = _notificationPanel.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            _notificationText = CreateText(panelRect, "NotificationText", "",
                TextAlignmentOptions.Center, 24, Color.white);
            var textRect = _notificationText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 10);
            textRect.offsetMax = new Vector2(-20, -10);

            _notificationPanel.SetActive(false);
        }
        #endregion

        #region UI Helpers
        private RectTransform CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(_canvas.transform);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            return rect;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text,
            TextAlignmentOptions alignment, float fontSize, Color color)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.fontSize = fontSize;
            tmp.color = color;

            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string text, Color bgColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = bgColor;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(onClick);

            var btnText = CreateText(btnObj.transform, "Text", text,
                TextAlignmentOptions.Center, 22, Color.white);

            return btn;
        }
        #endregion

        #region Event Handlers
        private void OnConnected()
        {
            Debug.Log("[UIManager] OnConnected called!");
            if (_connectionStatus == null)
            {
                Debug.LogError("[UIManager] _connectionStatus is null!");
                return;
            }
            _connectionStatus.text = "● Connected";
            _connectionStatus.color = Color.green;
            ShowNotification("Connected to The Island!", Color.green);
        }

        private void OnDisconnected()
        {
            _connectionStatus.text = "● Disconnected";
            _connectionStatus.color = Color.red;
            ShowNotification("Disconnected from server", Color.red);
        }

        private void OnTick(TickData data)
        {
            // If we're receiving tick events, we ARE connected
            EnsureConnectedStatus();

            _currentDay = data.day;
            _currentTick = data.tick;
            _aliveCount = data.alive_agents;
            UpdateTickInfo();
        }

        private void EnsureConnectedStatus()
        {
            // If status shows disconnected but we're receiving events, update it
            if (_connectionStatus != null && _connectionStatus.color == Color.red)
            {
                OnConnected();
            }
        }

        private void OnFeed(FeedEventData data)
        {
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.user_gold;
                UpdateGoldDisplay();
            }
            ShowNotification(data.message, new Color(1f, 0.7f, 0.3f));
        }

        private void OnUserUpdate(UserUpdateData data)
        {
            if (data.user == NetworkManager.Instance.Username)
            {
                _playerGold = data.gold;
                UpdateGoldDisplay();
            }
        }

        private void OnAgentDied(AgentDiedData data)
        {
            ShowNotification(data.message, Color.red);
        }

        private void OnSystemMessage(SystemEventData data)
        {
            ShowNotification(data.message, Color.cyan);
        }

        private void OnAgentsUpdate(System.Collections.Generic.List<AgentData> agents)
        {
            // If we're receiving agents events, we ARE connected
            EnsureConnectedStatus();

            if (agents != null)
            {
                _aliveCount = 0;
                foreach (var agent in agents)
                {
                    if (agent.IsAlive) _aliveCount++;
                }
                UpdateTickInfo();
            }
        }

        private void OnCommandSubmit(string text)
        {
            SendCommand();
        }

        private void OnSendClicked()
        {
            SendCommand();
        }

        private void OnResetClicked()
        {
            NetworkManager.Instance.ResetGame();
            ShowNotification("Reset requested...", Color.yellow);
        }
        #endregion

        #region UI Updates
        private void UpdateAllUI()
        {
            UpdateGoldDisplay();
            UpdateTickInfo();
        }

        private void UpdateGoldDisplay()
        {
            if (_goldDisplay != null)
            {
                _goldDisplay.text = $"[G] {_playerGold} Gold";
            }
        }

        private void UpdateTickInfo()
        {
            if (_tickInfo != null)
            {
                _tickInfo.text = $"Day {_currentDay} | Tick {_currentTick} | Alive: {_aliveCount}";
            }
        }

        private void SendCommand()
        {
            if (_commandInput == null || string.IsNullOrWhiteSpace(_commandInput.text))
                return;

            NetworkManager.Instance.SendCommand(_commandInput.text);
            _commandInput.text = "";
            _commandInput.ActivateInputField();
        }

        public void ShowNotification(string message, Color color)
        {
            if (_notificationPanel == null || _notificationText == null) return;

            _notificationText.text = message;
            _notificationText.color = color;
            _notificationPanel.SetActive(true);

            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), 3f);
        }

        private void HideNotification()
        {
            if (_notificationPanel != null)
            {
                _notificationPanel.SetActive(false);
            }
        }
        #endregion
    }
}
