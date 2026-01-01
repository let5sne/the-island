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
            showSpeechBubble(data.agent_id, data.agent_name, data.text);
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
        <button class="feed-btn" onclick="feedAgent('${agent.name}')" ${isDead ? 'disabled' : ''}>
            🍖 投喂 (10金币)
        </button>
    `;

    return card;
}

/**
 * Feed an agent
 */
function feedAgent(agentName) {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('未连接到服务器');
        return;
    }

    const user = getCurrentUser();
    const payload = {
        action: 'send_comment',
        payload: { user, message: `feed ${agentName}` }
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
            return `Tick #${data.tick} | 第${data.day}天 | 存活: ${data.alive_agents}人`;
        case 'system':
        case 'error':
        case 'feed':
        case 'agent_died':
        case 'check':
            return data.message;
        case 'agent_speak':
            return `💬 ${data.agent_name}: "${data.text}"`;
        case 'agents_update':
            return `角色状态已更新`;
        case 'user_update':
            return `${data.user} 金币: ${data.gold}`;
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
