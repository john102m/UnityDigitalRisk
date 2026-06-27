using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance;

    public GameStateDTO State { get; private set; }
    public event Action OnStateChanged;

    SignalRClient signalR;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnGameStateUpdated += HandleStateUpdate;
    }

    void HandleStateUpdate(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        State = JsonConvert.DeserializeObject<GameStateDTO>(json);
        Debug.Log($"[State #{++updateCount}] {State?.players?.Count} players, {State?.territories?.Count} territories, phase={State?.phase}/{State?.turnPhase}");
        OnStateChanged?.Invoke();
    }

    int updateCount = 0;
}

// DTOs matching server's SignalR payloads
[Serializable]
public class GameStateDTO
{
    public string gameCode;
    public string phase;
    public string turnPhase;
    public int currentPlayerIndex;
    public List<PlayerDTO> players;
    public List<TerritoryDTO> territories;
}

[Serializable]
public class PlayerDTO
{
    public string name;
    public string colour;
    public int avatarIndex;
    public bool isHost;
    public int reinforcementsRemaining;
    public bool isEliminated;
    public bool isAI;
}

[Serializable]
public class TerritoryDTO
{
    public int id;
    public string name;
    public string continent;
    public int ownerId;
    public int armies;
}
