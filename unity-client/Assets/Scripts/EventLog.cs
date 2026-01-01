using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheIsland.Network;
using TheIsland.Models;

namespace TheIsland.UI
{
    /// <summary>
    /// 事件日志面板 - 显示游戏事件历史
    /// </summary>
    public class EventLog : MonoBehaviour
    {
        private static EventLog _instance;
        public static EventLog Instance => _instance;

        // 自动创建 EventLog 实例
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance == null)
            {
                var go = new GameObject("EventLog");
                go.AddComponent<EventLog>();
                DontDestroyOnLoad(go);
                Debug.Log("[EventLog] 自动创建实例");
            }
        }

        // UI 组件
        private Canvas _canvas;
        private GameObject _panel;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private Button _toggleBtn;
        private TextMeshProUGUI _toggleText;
        private List<GameObject> _entries = new List<GameObject>();
        private bool _visible = true;
        private int _unread = 0;

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
            Debug.Log("[EventLog] Start - 开始初始化");
            StartCoroutine(InitCoroutine());
        }

        private System.Collections.IEnumerator InitCoroutine()
        {
            // 等待一帧确保 Canvas 准备好
            yield return null;

            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("[EventLog] 找不到 Canvas");
                yield break;
            }

            Debug.Log($"[EventLog] 找到 Canvas: {_canvas.name}");
            BuildUI();

            // 等待 NetworkManager
            int retries = 0;
            while (NetworkManager.Instance == null && retries < 20)
            {
                Debug.Log("[EventLog] 等待 NetworkManager...");
                yield return new WaitForSeconds(0.25f);
                retries++;
            }

            if (NetworkManager.Instance != null)
            {
                SubscribeEvents();
                AddLog("事件日志已就绪", Color.yellow);
                Debug.Log("[EventLog] 初始化完成");
            }
            else
            {
                Debug.LogError("[EventLog] NetworkManager 超时");
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                var n = NetworkManager.Instance;
                n.OnConnected -= OnConnected;
                n.OnAgentSpeak -= OnSpeak;
                n.OnSocialInteraction -= OnSocial;
                n.OnAgentDied -= OnDeath;
                n.OnFeed -= OnFeed;
                n.OnHeal -= OnHeal;
                n.OnEncourage -= OnEncourage;
                n.OnRevive -= OnRevive;
                n.OnTalk -= OnTalk;
                n.OnSystemMessage -= OnSystem;
                n.OnWeatherChange -= OnWeather;
                n.OnPhaseChange -= OnPhase;
                n.OnDayChange -= OnDay;
            }
        }

        private void SubscribeEvents()
        {
            var n = NetworkManager.Instance;
            n.OnConnected += OnConnected;
            n.OnAgentSpeak += OnSpeak;
            n.OnSocialInteraction += OnSocial;
            n.OnAgentDied += OnDeath;
            n.OnFeed += OnFeed;
            n.OnHeal += OnHeal;
            n.OnEncourage += OnEncourage;
            n.OnRevive += OnRevive;
            n.OnTalk += OnTalk;
            n.OnSystemMessage += OnSystem;
            n.OnWeatherChange += OnWeather;
            n.OnPhaseChange += OnPhase;
            n.OnDayChange += OnDay;
            Debug.Log("[EventLog] 事件订阅完成");
        }

        // 事件处理
        private void OnConnected() => AddLog("已连接服务器", Color.green);
        private void OnSpeak(AgentSpeakData d) => AddLog($"{d.agent_name}: \"{d.text}\"", Color.white);
        private void OnSocial(SocialInteractionData d) => AddLog($"{d.initiator_name} -> {d.target_name}: {d.dialogue}", new Color(0.6f, 0.9f, 1f));
        private void OnDeath(AgentDiedData d) => AddLog($"{d.agent_name} 死亡!", Color.red);
        private void OnFeed(FeedEventData d) => AddLog($"{d.user} 喂食 {d.agent_name}", new Color(0.5f, 1f, 0.5f));
        private void OnHeal(HealEventData d) => AddLog($"{d.user} 治疗 {d.agent_name}", new Color(0.5f, 1f, 0.5f));
        private void OnEncourage(EncourageEventData d) => AddLog($"{d.user} 鼓励 {d.agent_name}", new Color(0.5f, 1f, 0.5f));
        private void OnRevive(ReviveEventData d) => AddLog($"{d.user} 复活 {d.agent_name}", Color.cyan);
        private void OnTalk(TalkEventData d) => AddLog($"{d.agent_name}: \"{d.response}\"", Color.white);
        private void OnSystem(SystemEventData d) => AddLog(d.message, Color.yellow);
        private void OnWeather(WeatherChangeData d) => AddLog($"天气: {d.new_weather}", new Color(0.7f, 0.85f, 1f));
        private void OnPhase(PhaseChangeData d) => AddLog($"时间: {d.new_phase}", new Color(1f, 0.9f, 0.7f));
        private void OnDay(DayChangeData d) => AddLog($"第 {d.day} 天!", new Color(1f, 0.9f, 0.7f));

        private void BuildUI()
        {
            // 切换按钮
            var toggleObj = new GameObject("LogToggle");
            toggleObj.transform.SetParent(_canvas.transform, false);
            var toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 1);
            toggleRect.anchorMax = new Vector2(0, 1);
            toggleRect.pivot = new Vector2(0, 1);
            toggleRect.anchoredPosition = new Vector2(10, -75);
            toggleRect.sizeDelta = new Vector2(90, 24);

            var toggleImg = toggleObj.AddComponent<Image>();
            toggleImg.color = new Color(0.2f, 0.35f, 0.5f, 0.9f);
            _toggleBtn = toggleObj.AddComponent<Button>();
            _toggleBtn.targetGraphic = toggleImg;
            _toggleBtn.onClick.AddListener(Toggle);

            var toggleTextObj = new GameObject("Text");
            toggleTextObj.transform.SetParent(toggleObj.transform, false);
            _toggleText = toggleTextObj.AddComponent<TextMeshProUGUI>();
            _toggleText.text = "隐藏日志";
            _toggleText.fontSize = 12;
            _toggleText.color = Color.white;
            _toggleText.alignment = TextAlignmentOptions.Center;
            var ttRect = toggleTextObj.GetComponent<RectTransform>();
            ttRect.anchorMin = Vector2.zero;
            ttRect.anchorMax = Vector2.one;
            ttRect.sizeDelta = Vector2.zero;

            // 主面板
            _panel = new GameObject("LogPanel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.offsetMin = new Vector2(10, 80);
            panelRect.offsetMax = new Vector2(360, -80);

            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.0f); // 完全透明背景

            // 标题
            var header = new GameObject("Header");
            header.transform.SetParent(_panel.transform, false);
            var headerRect = header.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 28);
            headerRect.anchoredPosition = Vector2.zero;

            header.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.8f);

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "事件日志";
            titleTmp.fontSize = 14;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(8, 0);
            titleRect.offsetMax = new Vector2(-50, 0);

            // 清除按钮
            var clearObj = new GameObject("Clear");
            clearObj.transform.SetParent(header.transform, false);
            var clearRect = clearObj.AddComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(1, 0.5f);
            clearRect.anchorMax = new Vector2(1, 0.5f);
            clearRect.pivot = new Vector2(1, 0.5f);
            clearRect.sizeDelta = new Vector2(45, 20);
            clearRect.anchoredPosition = new Vector2(-4, 0);

            var clearImg = clearObj.AddComponent<Image>();
            clearImg.color = new Color(0.5f, 0.25f, 0.25f);
            var clearBtn = clearObj.AddComponent<Button>();
            clearBtn.targetGraphic = clearImg;
            clearBtn.onClick.AddListener(Clear);

            var clearTextObj = new GameObject("Text");
            clearTextObj.transform.SetParent(clearObj.transform, false);
            var clearTmp = clearTextObj.AddComponent<TextMeshProUGUI>();
            clearTmp.text = "清除";
            clearTmp.fontSize = 11;
            clearTmp.color = Color.white;
            clearTmp.alignment = TextAlignmentOptions.Center;
            var ctRect = clearTextObj.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;

            // 滚动区域
            var scrollObj = new GameObject("Scroll");
            scrollObj.transform.SetParent(_panel.transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(4, 4);
            scrollRect.offsetMax = new Vector2(-4, -32);

            scrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = true;

            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 25;
            _scrollRect.viewport = scrollRect;

            // 内容容器
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            _content = contentObj.AddComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 0);

            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _content;

            Debug.Log("[EventLog] UI 构建完成");
        }

        public void AddLog(string msg, Color color)
        {
            if (_content == null) return;

            var entry = new GameObject($"E{_entries.Count}");
            entry.transform.SetParent(_content, false);

            entry.AddComponent<Image>().color = _entries.Count % 2 == 0
                ? new Color(0f, 0f, 0f, 0.2f)
                : new Color(0f, 0f, 0f, 0.1f);

            var le = entry.AddComponent<LayoutElement>();
            le.minHeight = 36;
            le.preferredHeight = 36;

            // 颜色条
            var bar = new GameObject("Bar");
            bar.transform.SetParent(entry.transform, false);
            var barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(0, 1);
            barRect.pivot = new Vector2(0, 0.5f);
            barRect.sizeDelta = new Vector2(3, -4);
            barRect.anchoredPosition = new Vector2(1, 0);
            bar.AddComponent<Image>().color = color;

            // 文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(entry.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            string time = System.DateTime.Now.ToString("HH:mm:ss");
            tmp.text = $"<color=#666><size=10>{time}</size></color> {msg}";
            tmp.fontSize = 12;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.richText = true;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 2);
            textRect.offsetMax = new Vector2(-4, -2);

            _entries.Add(entry);

            while (_entries.Count > 100)
            {
                Destroy(_entries[0]);
                _entries.RemoveAt(0);
            }

            if (!_visible)
            {
                _unread++;
                UpdateToggle();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0;
        }

        private void Clear()
        {
            foreach (var e in _entries) Destroy(e);
            _entries.Clear();
            _unread = 0;
            UpdateToggle();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            AddLog("已清除", Color.yellow);
        }

        private void Toggle()
        {
            _visible = !_visible;
            _panel.SetActive(_visible);
            if (_visible) _unread = 0;
            UpdateToggle();
        }

        private void UpdateToggle()
        {
            if (_toggleText != null)
                _toggleText.text = _visible ? "隐藏日志" : (_unread > 0 ? $"日志({_unread})" : "显示日志");
        }
    }
}
