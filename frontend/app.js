/**
 * The Island - Survival Simulation Client
 * Handles WebSocket connection, agent display, and user interactions
 */

let ws = null;
const WS_URL = 'ws://localhost:8080/ws';

// User state
let userGold = 100;

// Agents state
let agents = [];

// World state
let worldState = {
    day_count: 1,
    time_of_day: 'day',
    weather: 'Sunny'
};

// DOM Elements
const statusDot = document.getElementById('statusDot');
const statusText = document.getElementById('statusText');
const connectBtn = document.getElementById('connectBtn');
const eventLog = document.getElementById('eventLog');
const usernameInput = document.getElementById('username');
const messageInput = document.getElementById('message');
const autoScrollCheckbox = document.getElementById('autoScroll');
const hideTicksCheckbox = document.getElementById('hideTicks');
const agentsGrid = document.getElementById('agentsGrid');
const userGoldDisplay = document.getElementById('userGold');

/**
 * Toggle WebSocket connection
 */
function toggleConnection() {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.close();
    } else {
        connect();
    }
}

/**
 * Establish WebSocket connection
 */
function connect() {
    statusText.textContent = '连接中...';
    connectBtn.disabled = true;

    ws = new WebSocket(WS_URL);

    ws.onopen = () => {
        statusDot.classList.add('connected');
        statusText.textContent = '已连接';
        connectBtn.textContent = '断开';
        connectBtn.disabled = false;
        logEvent({ event_type: 'system', data: { message: '已连接到荒岛服务器' } });
    };

    ws.onclose = () => {
        statusDot.classList.remove('connected');
        statusText.textContent = '未连接';
        connectBtn.textContent = '连接';
        connectBtn.disabled = false;
        logEvent({ event_type: 'system', data: { message: '与服务器断开连接' } });
    };

    ws.onerror = (error) => {
        console.error('WebSocket error:', error);
        logEvent({ event_type: 'error', data: { message: '连接错误' } });
    };

    ws.onmessage = (event) => {
        try {
            const data = JSON.parse(event.data);
            handleGameEvent(data);
        } catch (e) {
            console.error('Failed to parse message:', e);
        }
    };
}

/**
 * Handle incoming game events
 */
function handleGameEvent(event) {
    const eventType = event.event_type;
    const data = event.data || {};

    switch (eventType) {
        case 'agents_update':
            updateAgentsUI(data.agents);
            break;
        case 'feed':
        case 'heal':
        case 'encourage':
        case 'revive':
        case 'user_update':
            updateUserGold(data);
            break;
        case 'check':
            if (data.user && data.user.username === getCurrentUser()) {
                userGold = data.user.gold;
                userGoldDisplay.textContent = userGold;
            }
            break;
        case 'agent_speak':
        case 'talk':
            if (data.agent_id !== undefined) {
                showSpeechBubble(data.agent_id, data.agent_name, data.text || data.response);
            }
            break;
        case 'social_interaction':
            showSocialInteraction(data);
            break;
        case 'world_update':
            updateWorldState(data);
            break;
        case 'weather_change':
            worldState.weather = data.new_weather;
            updateWorldDisplay();
            break;
        case 'phase_change':
            worldState.time_of_day = data.new_phase;
            updateWorldDisplay();
            break;
        case 'day_change':
            worldState.day_count = data.day;
            updateWorldDisplay();
            break;
        case 'tick':
            // Update world state from tick data
            if (data.day) worldState.day_count = data.day;
            if (data.time_of_day) worldState.time_of_day = data.time_of_day;
            if (data.weather) worldState.weather = data.weather;
            updateWorldDisplay();
            break;
    }

    logEvent(event);
}

/**
 * Get current username
 */
function getCurrentUser() {
    return usernameInput.value.trim() || '观众001';
}

/**
 * Update user gold display
 */
function updateUserGold(data) {
    if (data.user === getCurrentUser() && data.gold !== undefined) {
        userGold = data.gold;
        userGoldDisplay.textContent = userGold;
    }
    if (data.user_gold !== undefined && data.user === getCurrentUser()) {
        userGold = data.user_gold;
        userGoldDisplay.textContent = userGold;
    }
}

/**
 * Update agents UI with card view
 */
function updateAgentsUI(agentsData) {
    if (!agentsData || agentsData.length === 0) return;

    agents = agentsData;
    agentsGrid.innerHTML = '';

    agents.forEach(agent => {
        const card = createAgentCard(agent);
        agentsGrid.appendChild(card);
    });
}

/**
 * Create an agent card element
 */
function createAgentCard(agent) {
    const isDead = agent.status !== 'Alive';
    const card = document.createElement('div');
    card.className = `agent-card ${isDead ? 'dead' : ''}`;
    card.id = `agent-${agent.id}`;

    const statusClass = isDead ? 'dead' : 'alive';
    const statusText = isDead ? '已死亡' : '存活';

    // Mood emoji and color
    const moodEmoji = getMoodEmoji(agent.mood_state);
    const moodColor = getMoodColor(agent.mood_state);

    card.innerHTML = `
        <div class="agent-header">
            <div>
                <span class="agent-name">${agent.name}</span>
                <span class="agent-personality">${agent.personality}</span>
            </div>
            <span class="agent-status ${statusClass}">${statusText}</span>
        </div>
        <div class="stat-bar-container">
            <div class="stat-bar-label">
                <span>❤️ 生命值</span>
                <span>${agent.hp}/100</span>
            </div>
            <div class="stat-bar">
                <div class="stat-bar-fill hp" style="width: ${agent.hp}%"></div>
            </div>
        </div>
        <div class="stat-bar-container">
            <div class="stat-bar-label">
                <span>⚡ 体力</span>
                <span>${agent.energy}/100</span>
            </div>
            <div class="stat-bar">
                <div class="stat-bar-fill energy" style="width: ${agent.energy}%"></div>
            </div>
        </div>
        <div class="stat-bar-container">
            <div class="stat-bar-label">
                <span>${moodEmoji} 心情</span>
                <span>${agent.mood}/100</span>
            </div>
            <div class="stat-bar">
                <div class="stat-bar-fill" style="width: ${agent.mood}%; background: ${moodColor}"></div>
            </div>
        </div>
        <div class="agent-actions">
            ${isDead ? `
                <button class="action-btn revive" onclick="reviveAgent('${agent.name}')">
                    💫 复活 (10g)
                </button>
            ` : `
                <button class="action-btn feed" onclick="feedAgent('${agent.name}')">
                    🍖 投喂 (10g)
                </button>
                <button class="action-btn heal" onclick="healAgent('${agent.name}')">
                    💊 治疗 (15g)
                </button>
                <button class="action-btn encourage" onclick="encourageAgent('${agent.name}')">
                    💪 鼓励 (5g)
                </button>
                <button class="action-btn talk" onclick="talkToAgent('${agent.name}')">
                    💬 交谈
                </button>
            `}
        </div>
    `;

    return card;
}

/**
 * Get mood emoji based on mood state
 */
function getMoodEmoji(moodState) {
    const emojis = {
        'happy': '😊',
        'neutral': '😐',
        'sad': '😢',
        'anxious': '😰'
    };
    return emojis[moodState] || '😐';
}

/**
 * Get mood color based on mood state
 */
function getMoodColor(moodState) {
    const colors = {
        'happy': '#4ade80',
        'neutral': '#fbbf24',
        'sad': '#60a5fa',
        'anxious': '#f87171'
    };
    return colors[moodState] || '#fbbf24';
}

/**
 * Update world state from server
 */
function updateWorldState(data) {
    if (data.day_count) worldState.day_count = data.day_count;
    if (data.time_of_day) worldState.time_of_day = data.time_of_day;
    if (data.weather) worldState.weather = data.weather;
    updateWorldDisplay();
}

/**
 * Update the world display panel
 */
function updateWorldDisplay() {
    const worldDisplay = document.getElementById('worldDisplay');
    if (!worldDisplay) return;

    const weatherEmojis = {
        'Sunny': '☀️',
        'Cloudy': '☁️',
        'Rainy': '🌧️',
        'Stormy': '⛈️',
        'Hot': '🔥',
        'Foggy': '🌫️'
    };

    const phaseEmojis = {
        'dawn': '🌅',
        'day': '☀️',
        'dusk': '🌇',
        'night': '🌙'
    };

    const phaseNames = {
        'dawn': '黎明',
        'day': '白天',
        'dusk': '黄昏',
        'night': '夜晚'
    };

    worldDisplay.innerHTML = `
        <span>📅 第${worldState.day_count}天</span>
        <span>${phaseEmojis[worldState.time_of_day] || '☀️'} ${phaseNames[worldState.time_of_day] || '白天'}</span>
        <span>${weatherEmojis[worldState.weather] || '☀️'} ${worldState.weather}</span>
    `;
}

/**
 * Show social interaction notification
 */
function showSocialInteraction(data) {
    const interactionNames = {
        'chat': '聊天',
        'share_food': '分享食物',
        'help': '互相帮助',
        'argue': '争吵',
        'comfort': '安慰'
    };

    const message = `${data.initiator_name} 和 ${data.target_name} ${interactionNames[data.interaction_type] || '互动'}了`;
    logEvent({
        event_type: 'social_interaction',
        timestamp: Date.now() / 1000,
        data: { message, dialogue: data.dialogue }
    });
}

/**
 * Feed an agent
 */
function feedAgent(agentName) {
    sendCommand(`feed ${agentName}`);
}

/**
 * Heal an agent
 */
function healAgent(agentName) {
    sendCommand(`heal ${agentName}`);
}

/**
 * Encourage an agent
 */
function encourageAgent(agentName) {
    sendCommand(`encourage ${agentName}`);
}

/**
 * Talk to an agent
 */
function talkToAgent(agentName) {
    const topic = prompt(`想和 ${agentName} 聊什么？（留空则随便聊聊）`);
    if (topic !== null) {
        sendCommand(`talk ${agentName} ${topic}`.trim());
    }
}

/**
 * Revive a dead agent
 */
function reviveAgent(agentName) {
    sendCommand(`revive ${agentName}`);
}

/**
 * Send a command to the server
 */
function sendCommand(command) {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('未连接到服务器');
        return;
    }

    const user = getCurrentUser();
    const payload = {
        action: 'send_comment',
        payload: { user, message: command }
    };

    ws.send(JSON.stringify(payload));
}

/**
 * Show speech bubble above an agent card
 */
function showSpeechBubble(agentId, agentName, text) {
    const card = document.getElementById(`agent-${agentId}`);
    const overlay = document.getElementById('speechBubblesOverlay');

    if (!card || !overlay) {
        console.warn(`Agent card or overlay not found: agent-${agentId}`);
        return;
    }

    // Remove existing bubble for this agent if any
    const existingBubble = document.getElementById(`bubble-${agentId}`);
    if (existingBubble) {
        existingBubble.remove();
    }

    // Get card position relative to overlay
    const cardRect = card.getBoundingClientRect();
    const overlayRect = overlay.parentElement.getBoundingClientRect();

    // Create new speech bubble
    const bubble = document.createElement('div');
    bubble.className = 'speech-bubble';
    bubble.id = `bubble-${agentId}`;
    bubble.innerHTML = `
        <div class="bubble-name">${agentName}</div>
        <div>${text}</div>
    `;

    // Position bubble above the card
    const left = (cardRect.left - overlayRect.left) + (cardRect.width / 2);
    const top = (cardRect.top - overlayRect.top) - 10;

    bubble.style.left = `${left}px`;
    bubble.style.top = `${top}px`;

    overlay.appendChild(bubble);

    // Auto-hide after 5 seconds
    setTimeout(() => {
        bubble.classList.add('fade-out');
        setTimeout(() => {
            if (bubble.parentNode) {
                bubble.remove();
            }
        }, 300); // Wait for fade animation
    }, 5000);
}

/**
 * Reset game - revive all agents
 */
function resetGame() {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('未连接到服务器');
        return;
    }

    const user = getCurrentUser();
    const payload = {
        action: 'send_comment',
        payload: { user, message: 'reset' }
    };

    ws.send(JSON.stringify(payload));
}

/**
 * Send a comment/command to the server
 */
function sendComment() {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('未连接到服务器');
        return;
    }

    const user = getCurrentUser();
    const message = messageInput.value.trim();

    if (!message) {
        alert('请输入指令');
        return;
    }

    const payload = {
        action: 'send_comment',
        payload: { user, message }
    };

    ws.send(JSON.stringify(payload));
    messageInput.value = '';
}

/**
 * Format timestamp for display
 */
function formatTime(timestamp) {
    const date = new Date(timestamp * 1000);
    return date.toLocaleTimeString();
}

/**
 * Format event data for display
 */
function formatEventData(eventType, data) {
    switch (eventType) {
        case 'comment':
            return `${data.user}: ${data.message}`;
        case 'tick':
            const phaseEmoji = { 'dawn': '🌅', 'day': '☀️', 'dusk': '🌇', 'night': '🌙' };
            const weatherEmoji = { 'Sunny': '☀️', 'Cloudy': '☁️', 'Rainy': '🌧️', 'Stormy': '⛈️', 'Hot': '🔥', 'Foggy': '🌫️' };
            return `第${data.day}天 ${phaseEmoji[data.time_of_day] || ''}${data.time_of_day} | ${weatherEmoji[data.weather] || ''}${data.weather} | 存活: ${data.alive_agents}人`;
        case 'system':
        case 'error':
        case 'feed':
        case 'heal':
        case 'encourage':
        case 'revive':
        case 'auto_revive':
        case 'agent_died':
        case 'check':
            return data.message;
        case 'agent_speak':
            return `💬 ${data.agent_name}: "${data.text}"`;
        case 'talk':
            return `💬 ${data.agent_name} 对 ${data.user} 说: "${data.response}"`;
        case 'agents_update':
            return `角色状态已更新`;
        case 'user_update':
            return `${data.user} 金币: ${data.gold}`;
        case 'weather_change':
            return `🌤️ 天气变化: ${data.old_weather} → ${data.new_weather}`;
        case 'phase_change':
            return `🕐 ${data.message}`;
        case 'day_change':
            return `📅 ${data.message}`;
        case 'social_interaction':
            const dialogue = data.dialogue ? `\n"${data.dialogue}"` : '';
            return `👥 ${data.message}${dialogue}`;
        case 'world_update':
            return `🌍 世界状态已更新`;
        default:
            return JSON.stringify(data);
    }
}

/**
 * Log an event to the display
 */
function logEvent(event) {
    const eventType = event.event_type || 'unknown';
    const timestamp = event.timestamp || Date.now() / 1000;
    const data = event.data || {};

    // Skip tick events if checkbox is checked
    if (eventType === 'tick' && hideTicksCheckbox && hideTicksCheckbox.checked) {
        return;
    }

    // Skip agents_update to reduce noise
    if (eventType === 'agents_update') {
        return;
    }

    const div = document.createElement('div');
    div.className = `event ${eventType}`;
    div.innerHTML = `
        <span class="event-time">${formatTime(timestamp)}</span>
        <span class="event-type">${eventType}</span>
        <div class="event-data">${formatEventData(eventType, data)}</div>
    `;

    eventLog.appendChild(div);

    if (autoScrollCheckbox.checked) {
        eventLog.scrollTop = eventLog.scrollHeight;
    }
}

/**
 * Clear the event log
 */
function clearLog() {
    eventLog.innerHTML = '';
}

// Allow Enter key to send comment
messageInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') {
        sendComment();
    }
});
