# The Island - Unity Client Setup Guide

## Prerequisites

- Unity 6000.3.2f1 or later
- Python backend running (`python run.py`)
- NativeWebSocket package installed

---

## Step 1: Install NativeWebSocket

### Option A: Via Package Manager (Recommended)
1. Open Unity
2. Go to **Window > Package Manager**
3. Click **+ > Add package from git URL**
4. Enter: `https://github.com/endel/NativeWebSocket.git#upm`
5. Click **Add**

### Option B: Via manifest.json
Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.endel.nativewebsocket": "https://github.com/endel/NativeWebSocket.git#upm"
  }
}
```

---

## Step 2: Import Scripts

1. Copy the `Assets/Scripts` folder into your Unity project
2. Unity will auto-compile the scripts

---

## Step 3: Create the Agent Prefab

### 3.1 Create Base Structure
1. **Create > 3D Object > Capsule** (or import your character model)
2. Rename to `AgentPrefab`
3. Add **Box Collider** component (for click detection)

### 3.2 Create World Space Canvas (for UI)
1. Right-click on `AgentPrefab` > **UI > Canvas**
2. Set Canvas **Render Mode** to `World Space`
3. Scale Canvas: `0.01, 0.01, 0.01`
4. Position above character: `Y = 2`

### 3.3 Add UI Elements to Canvas

```
AgentPrefab
├── Capsule (or Model)
├── Canvas (World Space)
│   ├── NameLabel (TextMeshPro)
│   │   └── Position: (0, 50, 0)
│   │   └── Text: "Agent Name"
│   │   └── Font Size: 24
│   │   └── Alignment: Center
│   │
│   ├── PersonalityLabel (TextMeshPro)
│   │   └── Position: (0, 30, 0)
│   │   └── Font Size: 16
│   │
│   ├── HPBar (Slider)
│   │   └── Position: (0, 10, 0)
│   │   └── Width: 100, Height: 10
│   │   └── Min: 0, Max: 1, Value: 1
│   │
│   ├── EnergyBar (Slider)
│   │   └── Position: (0, -5, 0)
│   │   └── Width: 100, Height: 10
│   │
│   └── SpeechBubble (Panel)
│       └── Position: (0, 80, 0)
│       └── Set Active: false (hidden by default)
│       ├── Background (Image)
│       │   └── Color: White with alpha
│       └── SpeechText (TextMeshPro)
│           └── Font Size: 14
│           └── Color: Black
```

### 3.4 Attach AgentController Script
1. Select `AgentPrefab`
2. **Add Component > AgentController**
3. Drag UI elements to the corresponding slots:
   - `Name Label` → NameLabel object
   - `HP Bar` → HPBar slider
   - `Energy Bar` → EnergyBar slider
   - `Speech Bubble` → SpeechBubble panel
   - `Speech Text` → SpeechText TextMeshPro

### 3.5 Create Prefab
1. Drag `AgentPrefab` from Hierarchy to `Assets/Prefabs` folder
2. Delete the instance from scene

---

## Step 4: Setup Scene Hierarchy

Create the following structure in your main scene:

```
Scene
├── Main Camera
├── Directional Light
├── NetworkManager (Empty GameObject)
│   └── Attach: NetworkManager.cs
│
├── GameManager (Empty GameObject)
│   └── Attach: GameManager.cs
│
├── AgentContainer (Empty GameObject)
│   └── This will hold spawned agents
│
└── UI Canvas (Screen Space - Overlay)
    ├── ConnectionStatus (TextMeshPro)
    ├── TickInfo (TextMeshPro)
    ├── GoldDisplay (TextMeshPro)
    ├── ResetButton (Button)
    ├── CommandInput (TMP InputField)
    ├── SendButton (Button)
    └── NotificationPanel (Panel)
        └── NotificationText (TextMeshPro)
```

---

## Step 5: Configure Components

### NetworkManager Configuration
1. Select `NetworkManager` object
2. In Inspector:
   - **Server URL**: `ws://localhost:8080/ws`
   - **Auto Connect**: ✓ (checked)
   - **Username**: Your player name

### GameManager Configuration
1. Select `GameManager` object
2. In Inspector:
   - **Agent Prefab**: Drag your AgentPrefab
   - **Agent Container**: Drag AgentContainer object
   - **Spawn Positions**: Set positions for agents
     - Element 0: `(-3, 0, 0)`
     - Element 1: `(0, 0, 0)`
     - Element 2: `(3, 0, 0)`
   - Drag all UI references

---

## Step 6: Test the Setup

1. **Start Python Backend**:
   ```bash
   cd the-island
   python run.py
   ```

2. **Play Unity Scene**:
   - Press Play in Unity Editor
   - Check Console for connection messages
   - Agents should spawn when data arrives

3. **Test Commands**:
   - Click on an agent to feed them
   - Use the command input to send `check` or `feed Jack`
   - Watch speech bubbles appear when agents talk

---

## Troubleshooting

### "NativeWebSocket not found"
- Ensure the package is installed correctly
- Check Package Manager for errors

### "No agents spawning"
- Verify Python backend is running on port 8080
- Check NetworkManager server URL
- Look for connection errors in Console

### "Speech bubbles not showing"
- Ensure SpeechBubble reference is assigned
- Check that the agent is alive (dead agents don't speak)

### "Click not working on agents"
- Add Collider component to agent prefab
- Ensure Main Camera has Physics Raycaster component

---

## Script API Reference

### NetworkManager
```csharp
// Send commands
NetworkManager.Instance.FeedAgent("Jack");
NetworkManager.Instance.CheckStatus();
NetworkManager.Instance.ResetGame();
NetworkManager.Instance.SendCommand("custom command");

// Events
NetworkManager.Instance.OnAgentsUpdate += (agents) => { };
NetworkManager.Instance.OnAgentSpeak += (data) => { };
```

### GameManager
```csharp
// Get agent by ID or name
AgentController agent = GameManager.Instance.GetAgent(1);
AgentController jack = GameManager.Instance.GetAgentByName("Jack");

// Properties
int gold = GameManager.Instance.PlayerGold;
int alive = GameManager.Instance.AliveAgentCount;
```

### AgentController
```csharp
// Update stats
agent.UpdateStats(agentData);

// Show speech
agent.ShowSpeech("Hello!");

// Properties
bool isAlive = agent.IsAlive;
AgentData data = agent.CurrentData;
```
