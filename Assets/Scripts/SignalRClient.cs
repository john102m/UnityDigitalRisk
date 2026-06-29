using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

/// <summary>
/// Connects to the Risk game server via SignalR and exposes game events as C# events.
/// SignalR callbacks arrive on background threads — all are marshalled to the main thread
/// via UnityMainThread before firing events.
/// </summary>
public class SignalRClient : MonoBehaviour
{
    [Tooltip("Full URL to the SignalR hub endpoint")]
    //public string serverUrl = "https://risk.spooch.co.uk/gamehub";
    public string serverUrl = "http://192.168.1.20:5000/gamehub";

    HubConnection connection;

    /// <summary>Fired when the server broadcasts updated game state (JSON string).</summary>
    public event Action<string> OnGameStateUpdated;

    /// <summary>Fired on each individual dice roll result (JSON string).</summary>
    public event Action<string> OnCombatResult;

    /// <summary>Fired when a blitz attack completes (JSON string with totals).</summary>
    public event Action<string> OnBlitzResult;

    /// <summary>Fired when a player selects attack source/target. -1 means not yet selected.</summary>
    public event Action<int, int> OnAttackSelection;

    /// <summary>Fired when server requests a dice roll from Unity (TV-driven physics).</summary>
    public event Action<int, int, int, int> OnCombatRollRequest;

    async void Start()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(serverUrl, options =>
            {
                // Allow both transports — WebSocket preferred, LongPolling as fallback
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                     Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol() // Must match server's System.Text.Json protocol
            .WithAutomaticReconnect()
            .Build();

        // Register event handlers — receive as JsonElement (matches server's System.Text.Json output)
        // then extract raw text for downstream deserialization with Newtonsoft

        connection.On<JsonElement>("GameStateUpdated", state =>
        {
            string json = state.GetRawText();
            Debug.Log($"[SignalR] GameStateUpdated received, length={json.Length}");
            UnityMainThread.Enqueue(() => OnGameStateUpdated?.Invoke(json));
        });

        connection.On<JsonElement>("CombatResult", result =>
        {
            string json = result.GetRawText();
            UnityMainThread.Enqueue(() => OnCombatResult?.Invoke(json));
        });

        connection.On<JsonElement>("BlitzResult", result =>
        {
            string json = result.GetRawText();
            UnityMainThread.Enqueue(() => OnBlitzResult?.Invoke(json));
        });

        // AttackSelection uses nullable ints — target is null when only source is selected
        connection.On<int?, int?>("AttackSelection", (sourceId, targetId) =>
        {
            UnityMainThread.Enqueue(() => OnAttackSelection?.Invoke(sourceId ?? -1, targetId ?? -1));
        });

        connection.On<JsonElement>("CombatRollRequest", request =>
        {
            int sourceId = request.GetProperty("sourceId").GetInt32();
            int targetId = request.GetProperty("targetId").GetInt32();
            int attackerCount = request.GetProperty("attackerDiceCount").GetInt32();
            int defenderCount = request.GetProperty("defenderDiceCount").GetInt32();
            Debug.Log($"[SignalR] CombatRollRequest: {attackerCount} attacker, {defenderCount} defender");
            UnityMainThread.Enqueue(() => OnCombatRollRequest?.Invoke(sourceId, targetId, attackerCount, defenderCount));
        });

        connection.Closed += error =>
        {
            Debug.Log($"SignalR disconnected: {error?.Message}");
            return Task.CompletedTask;
        };

        connection.Reconnected += id =>
        {
            Debug.Log($"SignalR reconnected: {id}");
            return Task.CompletedTask;
        };

        await Connect();
    }

    /// <summary>Establish connection, register as TV, and request initial game state.</summary>
    async Task Connect()
    {
        try
        {
            await connection.StartAsync();
            Debug.Log("SignalR connected");
            await connection.InvokeAsync("GetState");
            await connection.SendAsync("RegisterAsTV");
            Debug.Log("Registered as Unity TV");
            _ = PollServerState();
        }
        catch (Exception ex)
        {
            Debug.LogError($"SignalR connection failed: {ex.Message}");
        }
    }

    /// <summary>Send dice roll results back to server after physics simulation.</summary>
    public async Task SendDiceResult(int sourceId, int targetId, int[] attackerDice, int[] defenderDice)
    {
        if (connection?.State == HubConnectionState.Connected)
            await connection.InvokeAsync("SubmitDiceResult", attackerDice, defenderDice);
    }

    /// <summary>
    /// Safety net — requests full state every 5 seconds in case a broadcast is missed.
    /// WebSocket connections can occasionally drop individual messages under load.
    /// </summary>
    async Awaitable PollServerState()
    {
        while (true)
        {
            await Awaitable.WaitForSecondsAsync(5f);
            if (connection?.State == HubConnectionState.Connected)
                _ = connection.InvokeAsync("GetState");
        }
    }

    async void OnDestroy()
    {
        if (connection != null)
            await connection.DisposeAsync();
    }
}
