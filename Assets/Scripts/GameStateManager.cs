using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Singleton that holds the current deserialized game state.
/// Listens for GameStateUpdated from SignalRClient, deserializes JSON,
/// and fires OnStateChanged so UI/renderers can refresh.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance;

    public GameStateDTO State { get; private set; }

    /// <summary>Fired whenever the game state is updated from the server.</summary>
    public event Action OnStateChanged;

    SignalRClient signalR;
    int updateCount = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnGameStateUpdated += HandleStateUpdate;
    }

    /// <summary>Deserialize incoming JSON and notify listeners.</summary>
    void HandleStateUpdate(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        State = JsonConvert.DeserializeObject<GameStateDTO>(json);
        Debug.Log($"[State #{++updateCount}] {State?.players?.Count} players, {State?.territories?.Count} territories, phase={State?.phase}/{State?.turnPhase}");
        OnStateChanged?.Invoke();
    }
}

// --- DTOs matching server's camelCase JSON payloads ---

[Serializable]
public class GameStateDTO
{
    public string gameCode;
    public string phase;       // "Lobby", "InitialPlacement", "Playing", "GameOver"
    public string turnPhase;   // "Reinforce", "Attack", "Fortify"
    public int currentPlayerIndex;
    public List<PlayerDTO> players;
    public List<TerritoryDTO> territories;
}

[Serializable]
public class PlayerDTO
{
    public string name;
    public string colour;      // Hex colour e.g. "#E53E3E"
    public int avatarIndex;
    public bool isHost;
    public int reinforcementsRemaining;
    public bool isEliminated;
    public bool isAI;
}

[Serializable]
public class TerritoryDTO
{
    public int id;             // 0–41
    public string name;
    public string continent;
    public int ownerId;        // Index into players list, -1 if unowned
    public int armies;
}
