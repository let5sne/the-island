/**
 * The Island - Debug Client JavaScript
 * Handles WebSocket connection, UI interactions, and game state display
 */

let ws = null;
const WS_URL = 'ws://localhost:8080/ws';

// Player state (tracked from server events)
let playerState = {
    hp: 100,
    maxHp: 100,
    gold: 0
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

// Boss UI Elements
const bossName = document.getElementById('bossName');
const bossHpText = document.getElementById('bossHpText');
const bossHealthBar = document.getElementById('bossHealthBar');
const bossHealthLabel = document.getElementById('bossHealthLabel');

// Player UI Elements
const playerHpDisplay = document.getElementById('playerHp');
const playerGoldDisplay = document.getElementById('playerGold');

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
    statusText.textContent = 'Connecting...';
    connectBtn.disabled = true;

    ws = new WebSocket(WS_URL);

    ws.onopen = () => {
        statusDot.classList.add('connected');
        statusText.textContent = 'Connected';
        connectBtn.textContent = 'Disconnect';
        connectBtn.disabled = false;
        logEvent({ event_type: 'system', data: { message: 'WebSocket connected' } });
    };

    ws.onclose = () => {
        statusDot.classList.remove('connected');
        statusText.textContent = 'Disconnected';
        connectBtn.textContent = 'Connect';
        connectBtn.disabled = false;
        logEvent({ event_type: 'system', data: { message: 'WebSocket disconnected' } });
    };

    ws.onerror = (error) => {
        console.error('WebSocket error:', error);
        logEvent({ event_type: 'error', data: { message: 'Connection error' } });
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

    // Update UI based on event type
    switch (eventType) {
        case 'boss_update':
            updateBossUI(data);
            break;
        case 'attack':
            updateBossFromAttack(data);
            updatePlayerFromEvent(data);
            break;
        case 'heal':
            updatePlayerFromEvent(data);
            break;
        case 'status':
            updatePlayerFromEvent(data);
            updateBossFromStatus(data);
            break;
        case 'system':
            // Handle player death/respawn updates
            if (data.user && data.player_hp !== undefined) {
                updatePlayerFromEvent(data);
            }
            break;
        case 'tick':
            if (data.boss_hp !== undefined) {
                updateBossUI({
                    boss_hp: data.boss_hp,
                    boss_max_hp: data.boss_max_hp
                });
            }
            break;
    }

    // Log the event
    logEvent(event);
}

/**
 * Update Boss health bar UI
 */
function updateBossUI(data) {
    if (data.boss_name) {
        bossName.textContent = data.boss_name;
    }
    if (data.boss_hp !== undefined && data.boss_max_hp !== undefined) {
        const hp = data.boss_hp;
        const maxHp = data.boss_max_hp;
        const percentage = maxHp > 0 ? (hp / maxHp) * 100 : 0;

        bossHpText.textContent = `HP: ${hp} / ${maxHp}`;
        bossHealthBar.style.width = `${percentage}%`;
        bossHealthLabel.textContent = `${Math.round(percentage)}%`;

        // Change color based on HP percentage
        if (percentage <= 25) {
            bossHealthBar.style.background = 'linear-gradient(90deg, #ff2222 0%, #ff4444 100%)';
        } else if (percentage <= 50) {
            bossHealthBar.style.background = 'linear-gradient(90deg, #ff6600 0%, #ff8844 100%)';
        } else {
            bossHealthBar.style.background = 'linear-gradient(90deg, #ff4444 0%, #ff6666 100%)';
        }
    }
}

/**
 * Update boss from attack event
 */
function updateBossFromAttack(data) {
    if (data.boss_hp !== undefined && data.boss_max_hp !== undefined) {
        updateBossUI({
            boss_hp: data.boss_hp,
            boss_max_hp: data.boss_max_hp
        });
    }
}

/**
 * Update boss from status event
 */
function updateBossFromStatus(data) {
    if (data.boss_hp !== undefined && data.boss_max_hp !== undefined) {
        updateBossUI({
            boss_name: data.boss_name,
            boss_hp: data.boss_hp,
            boss_max_hp: data.boss_max_hp
        });
    }
}

/**
 * Update player stats from event data
 */
function updatePlayerFromEvent(data) {
    const currentUser = usernameInput.value.trim() || 'Anonymous';

    // Only update if this event is for the current user
    if (data.user !== currentUser) return;

    if (data.player_hp !== undefined) {
        playerState.hp = data.player_hp;
    }
    if (data.player_max_hp !== undefined) {
        playerState.maxHp = data.player_max_hp;
    }
    if (data.player_gold !== undefined) {
        playerState.gold = data.player_gold;
    }

    updatePlayerUI();
}

/**
 * Update player stats UI
 */
function updatePlayerUI() {
    playerHpDisplay.textContent = `${playerState.hp}/${playerState.maxHp}`;
    playerGoldDisplay.textContent = playerState.gold;
}

/**
 * Send a comment/command to the server
 */
function sendComment() {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('Not connected to server');
        return;
    }

    const user = usernameInput.value.trim() || 'Anonymous';
    const message = messageInput.value.trim();

    if (!message) {
        alert('Please enter a message');
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
 * Quick action buttons
 */
function quickAction(action) {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        alert('Not connected to server');
        return;
    }

    const user = usernameInput.value.trim() || 'Anonymous';

    const payload = {
        action: 'send_comment',
        payload: { user, message: action }
    };

    ws.send(JSON.stringify(payload));
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
        case 'agent_response':
            return data.response;
        case 'tick':
            return `Tick #${data.tick} | Players: ${data.player_count || 0}`;
        case 'system':
        case 'error':
            return data.message;
        case 'attack':
        case 'heal':
        case 'status':
        case 'boss_defeated':
            return data.message;
        case 'boss_update':
            return `Boss ${data.boss_name}: ${data.boss_hp}/${data.boss_max_hp} (${Math.round(data.boss_hp_percentage)}%)`;
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

    // Skip boss_update events to reduce log noise (they're reflected in the UI)
    if (eventType === 'boss_update') {
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
