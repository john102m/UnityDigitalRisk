using UnityEngine;
using TMPro;

/// <summary>
/// UI overlay displaying game info: game code, current phase, whose turn, and player list.
/// Refreshes automatically when GameStateManager fires OnStateChanged.
/// </summary>
public class InfoPanel : MonoBehaviour
{
    [Tooltip("Displays the game join code")]
    public TextMeshProUGUI gameCodeText;

    [Tooltip("Displays current game phase and turn phase")]
    public TextMeshProUGUI phaseText;

    [Tooltip("Displays active player list with coloured dots")]
    public TextMeshProUGUI playersText;

    [Tooltip("Displays whose turn it is")]
    public TextMeshProUGUI turnText;

    void Start()
    {
        GameStateManager.Instance.OnStateChanged += Refresh;
    }

    /// <summary>Update all text fields from current game state.</summary>
    void Refresh()
    {
        var state = GameStateManager.Instance.State;
        if (state == null) return;

        gameCodeText.text = $"Game: {state.gameCode}";
        phaseText.text = $"{state.phase} — {state.turnPhase}";

        // Current turn indicator
        if (state.currentPlayerIndex >= 0 && state.currentPlayerIndex < state.players.Count)
        {
            var p = state.players[state.currentPlayerIndex];
            turnText.text = $"{p.name}'s turn";
        }

        // Player list with coloured bullet (● character) — uses TMP rich text tags
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < state.players.Count; i++)
        {
            var p = state.players[i];
            if (p.isEliminated) continue;
            sb.AppendLine($"<color={p.colour}>\u25CF</color> {p.name}");
        }
        playersText.text = sb.ToString();
    }
}
