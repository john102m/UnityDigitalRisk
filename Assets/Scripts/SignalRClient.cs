using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

public class SignalRClient : MonoBehaviour
{
    public string serverUrl = "https://risk.spooch.co.uk/gamehub";

    HubConnection connection;

    public event Action<string> OnGameStateUpdated;
    public event Action<string> OnCombatResult;
    public event Action<string> OnBlitzResult;
    public event Action<int, int> OnAttackSelection;

    async void Start()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(serverUrl, options =>
            {
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                     Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol()
            .WithAutomaticReconnect()
            .Build();

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

        connection.On<int?, int?>("AttackSelection", (sourceId, targetId) =>
        {
            UnityMainThread.Enqueue(() => OnAttackSelection?.Invoke(sourceId ?? -1, targetId ?? -1));
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

    async Task Connect()
    {
        try
        {
            await connection.StartAsync();
            Debug.Log("SignalR connected");
            await connection.InvokeAsync("GetState");
            StartCoroutine(PollState());
        }
        catch (Exception ex)
        {
            Debug.LogError($"SignalR connection failed: {ex.Message}");
        }
    }

    System.Collections.IEnumerator PollState()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
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
