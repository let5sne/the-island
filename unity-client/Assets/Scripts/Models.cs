using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheIsland.Models
{
    /// <summary>
    /// Root server message structure.
    /// Matches Python's GameEvent schema.
    /// </summary>
    [Serializable]
    public class ServerMessage
    {
        public string event_type;
        public double timestamp;
        // data is parsed separately based on event_type
        // We use a raw JSON approach for flexibility
    }

    /// <summary>
    /// Wrapper for deserializing the full message including raw data.
    /// </summary>
    [Serializable]
    public class RawServerMessage
    {
        public string event_type;
        public double timestamp;
        public string rawData; // Will be populated manually after initial parse
    }

    /// <summary>
    /// Agent data from the server.
    /// Matches Python's Agent.to_dict() output.
    /// </summary>
    [Serializable]
    public class AgentData
    {
        public int id;
        public string name;
        public string personality;
        public string status;  // "Alive", "Dead", "Exiled"
        public int hp;
        public int energy;
        public string inventory;

        public bool IsAlive => status == "Alive";
    }

    /// <summary>
    /// Agents update event data.
    /// </summary>
    [Serializable]
    public class AgentsUpdateData
    {
        public List<AgentData> agents;
    }

    /// <summary>
    /// Agent speak event data (LLM response).
    /// </summary>
    [Serializable]
    public class AgentSpeakData
    {
        public int agent_id;
        public string agent_name;
        public string text;
    }

    /// <summary>
    /// Feed event data.
    /// </summary>
    [Serializable]
    public class FeedEventData
    {
        public string user;
        public string agent_name;
        public int energy_restored;
        public int agent_energy;
        public int user_gold;
        public string message;
    }

    /// <summary>
    /// Agent death event data.
    /// </summary>
    [Serializable]
    public class AgentDiedData
    {
        public string agent_name;
        public string message;
    }

    /// <summary>
    /// Tick event data.
    /// </summary>
    [Serializable]
    public class TickData
    {
        public int tick;
        public int day;
        public int alive_agents;
    }

    /// <summary>
    /// System/Error event data.
    /// </summary>
    [Serializable]
    public class SystemEventData
    {
        public string message;
    }

    /// <summary>
    /// User update event data.
    /// </summary>
    [Serializable]
    public class UserUpdateData
    {
        public string user;
        public int gold;
    }

    /// <summary>
    /// World state data.
    /// </summary>
    [Serializable]
    public class WorldStateData
    {
        public int day_count;
        public string weather;
        public int resource_level;
    }

    /// <summary>
    /// Client message structure for sending to server.
    /// </summary>
    [Serializable]
    public class ClientMessage
    {
        public string action;
        public ClientPayload payload;
    }

    [Serializable]
    public class ClientPayload
    {
        public string user;
        public string message;
    }

    /// <summary>
    /// Event type constants matching Python's EventType enum.
    /// </summary>
    public static class EventTypes
    {
        public const string COMMENT = "comment";
        public const string TICK = "tick";
        public const string SYSTEM = "system";
        public const string ERROR = "error";
        public const string AGENTS_UPDATE = "agents_update";
        public const string AGENT_DIED = "agent_died";
        public const string AGENT_SPEAK = "agent_speak";
        public const string FEED = "feed";
        public const string USER_UPDATE = "user_update";
        public const string WORLD_UPDATE = "world_update";
        public const string CHECK = "check";
    }
}
