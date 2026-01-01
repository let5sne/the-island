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

        // Mood system (Phase 3)
        public int mood;
        public string mood_state;  // "happy", "neutral", "sad", "anxious"
        public string social_tendency;  // "introvert", "extrovert", "neutral"

        // Survival (Phase 15)
        public bool is_sick;
        public int immunity;

        // Autonomous Agency (Phase 13)
        public string current_action;
        public string location;

        // Relationship 2.0 (Phase 17-B)
        public string social_role;  // "leader", "follower", "loner", "neutral"

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
        public string time_of_day;  // "dawn", "day", "dusk", "night"
        public string weather;      // "Sunny", "Cloudy", "Rainy", etc.
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
        public int current_tick_in_day;
        public string time_of_day;  // "dawn", "day", "dusk", "night"

        // Resource Scarcity (Phase 17-A)
        public int tree_left_fruit;
        public int tree_right_fruit;
    }

    /// <summary>
    /// Weather change event data.
    /// </summary>
    [Serializable]
    public class WeatherChangeData
    {
        public string old_weather;
        public string new_weather;
        public string message;
    }

    /// <summary>
    /// Phase change event data (day/night cycle).
    /// </summary>
    [Serializable]
    public class PhaseChangeData
    {
        public string old_phase;
        public string new_phase;
        public int day;
        public string message;
    }

    /// <summary>
    /// Day change event data.
    /// </summary>
    [Serializable]
    public class DayChangeData
    {
        public int day;
        public string message;
    }

    /// <summary>
    /// Heal event data.
    /// </summary>
    [Serializable]
    public class HealEventData
    {
        public string user;
        public string agent_name;
        public int hp_restored;
        public int agent_hp;
        public int user_gold;
        public string message;
    }

    /// <summary>
    /// Encourage event data.
    /// </summary>
    [Serializable]
    public class EncourageEventData
    {
        public string user;
        public string agent_name;
        public int mood_boost;
        public int agent_mood;
        public int user_gold;
        public string message;
    }

    /// <summary>
    /// Talk event data.
    /// </summary>
    [Serializable]
    public class TalkEventData
    {
        public string user;
        public string agent_name;
        public string topic;
        public string response;
    }

    /// <summary>
    /// Revive event data.
    /// </summary>
    [Serializable]
    public class ReviveEventData
    {
        public string user;
        public string agent_name;
        public int user_gold;
        public string message;
    }

    /// <summary>
    /// Social interaction event data.
    /// </summary>
    [Serializable]
    public class SocialInteractionData
    {
        public int initiator_id;
        public string initiator_name;
        public int target_id;
        public string target_name;
        public string interaction_type;  // "chat", "share_food", "help", "argue", "comfort"
        public string relationship_type;  // "stranger", "friend", "rival", etc.
        public string dialogue;
    }

    /// <summary>
    /// Gift effect event data (Twitch bits, subscriptions, etc.).
    /// </summary>
    [Serializable]
    public class GiftEffectData
    {
        public string user;
        public string gift_type;  // "bits", "heart", "sub"
        public int value;
        public string message;
        public string agent_name;  // Target agent for the effect
        public string gratitude;   // AI-generated thank you message
        public float duration;     // How long to show the speech bubble (default 8s)
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

        // Day/Night cycle (Phase 2)
        public const string TIME_UPDATE = "time_update";
        public const string PHASE_CHANGE = "phase_change";
        public const string DAY_CHANGE = "day_change";

        // Weather system (Phase 3)
        public const string WEATHER_CHANGE = "weather_change";
        public const string MOOD_UPDATE = "mood_update";

        // New commands (Phase 4)
        public const string HEAL = "heal";
        public const string TALK = "talk";
        public const string ENCOURAGE = "encourage";
        public const string REVIVE = "revive";

        // Social system (Phase 5)
        public const string SOCIAL_INTERACTION = "social_interaction";
        public const string RELATIONSHIP_CHANGE = "relationship_change";
        public const string AUTO_REVIVE = "auto_revive";

        // Gift/Donation system (Phase 8)
        public const string GIFT_EFFECT = "gift_effect";

        // Autonomous Agency (Phase 13)
        public const string AGENT_ACTION = "agent_action";

        // Crafting System (Phase 16)
        public const string CRAFT = "craft";
        public const string USE_ITEM = "use_item";

        // Random Events (Phase 17-C)
        public const string RANDOM_EVENT = "random_event";
    }

    /// <summary>
    /// Agent action event data (Phase 13).
    /// </summary>
    [Serializable]
    public class AgentActionData
    {
        public int agent_id;
        public string agent_name;
        public string action_type; // "Gather", "Sleep", "Socialize", "Wander", "Gather Herb", etc.
        public string location;    // "tree_left", "campfire", "herb_patch", etc.
        public string target_name; // For social actions
        public string dialogue;    // Bark text
    }

    /// <summary>
    /// Craft event data (Phase 16).
    /// </summary>
    [Serializable]
    public class CraftEventData
    {
        public int agent_id;
        public string agent_name;
        public string item;        // "medicine"
        public string ingredients; // JSON string of ingredients used
    }

    /// <summary>
    /// Use item event data (Phase 16).
    /// </summary>
    [Serializable]
    public class UseItemEventData
    {
        public int agent_id;
        public string agent_name;
        public string item;   // "medicine"
        public string effect; // "cured sickness"
    }

    /// <summary>
    /// Random event data (Phase 17-C).
    /// </summary>
    [Serializable]
    public class RandomEventData
    {
        public string event_type;   // "storm_damage", "treasure_found", "beast_attack", "rumor_spread"
        public string message;
        public string agent_name;   // Optional: affected agent
    }
}
