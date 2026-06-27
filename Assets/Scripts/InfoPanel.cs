using UnityEngine;
using TMPro;

public class InfoPanel : MonoBehaviour
{
    public TextMeshProUGUI gameCodeText;
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI playersText;
    public TextMeshProUGUI turnText;

    void Start()
    {
        GameStateManager.Instance.OnStateChanged += Refresh;
    }

    void Refresh()
    {
        var state = GameStateManager.Instance.State;
        if (state == null) return;

        gameCodeText.text = $"Game: {state.gameCode}";
        phaseText.text = $"{state.phase} — {state.turnPhase}";

        // Current turn
        if (state.currentPlayerIndex >= 0 && state.currentPlayerIndex < state.players.Count)
        {
            var p = state.players[state.currentPlayerIndex];
            turnText.text = $"{p.name}'s turn";
        }

        // Player list
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
