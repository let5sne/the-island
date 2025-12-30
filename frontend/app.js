/**
 * The Island - Debug Client JavaScript
 * Handles WebSocket connection and UI interactions
 */

let ws = null;
const WS_URL = 'ws://localhost:8000/ws';

// DOM Elements
const statusDot = document.getElementById('statusDot');
const statusText = document.getElementById('statusText');
const connectBtn = document.getElementById('connectBtn');
const eventLog = document.getElementById('eventLog');
const usernameInput = document.getElementById('username');
const messageInput = document.getElementById('message');
const autoScrollCheckbox = document.getElementById('autoScroll');

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
            logEvent(data);
        } catch (e) {
            console.error('Failed to parse message:', e);
        }
    };
}

/**
 * Send a mock comment to the server
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
            return `Tick #${data.tick}`;
        case 'system':
        case 'error':
            return data.message;
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
