using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class CombatTheatre : MonoBehaviour
{
    public Camera mainCamera;
    public Camera diceCamera;
    public DiceRoller diceRoller;
    public GameObject dicePanelUI; // RawImage showing dice RenderTexture

    Queue<CombatResultDTO> pendingCombats = new();
    bool isPlaying;

    void Start()
    {
        var signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnCombatResult += OnCombatResult;

        // Hide dice panel at start
        ShowDicePanel(false);
    }

    void ShowDicePanel(bool show)
    {
        if (diceCamera != null) diceCamera.enabled = show;
        if (dicePanelUI != null)
        {
            var cg = dicePanelUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = dicePanelUI.AddComponent<CanvasGroup>();
            cg.alpha = show ? 1f : 0f;
        }
    }

    void OnCombatResult(string json)
    {
        Debug.Log($"[CombatTheatre] Received combat result, length={json.Length}");
        var result = JsonConvert.DeserializeObject<CombatResultDTO>(json);
        if (result == null) return;

        Debug.Log($"[CombatTheatre] Attacker dice: {result.attackerDice?.Length}, Defender dice: {result.defenderDice?.Length}");
        pendingCombats.Enqueue(result);
        if (!isPlaying) StartCoroutine(ProcessQueue());
    }

    IEnumerator ProcessQueue()
    {
        isPlaying = true;
        while (pendingCombats.Count > 0)
        {
            yield return StartCoroutine(PlayCombatSequence(pendingCombats.Dequeue()));
        }
        isPlaying = false;
    }

    IEnumerator PlayCombatSequence(CombatResultDTO result)
    {
        Debug.Log("[CombatTheatre] Playing sequence - showing panel");
        ShowDicePanel(true);

        // Roll dice
        bool done = false;
        yield return diceRoller.StartCoroutine(
            diceRoller.RollDice(result.attackerDice, result.defenderDice, () => done = true));

        ShowDicePanel(false);
        diceRoller.ClearDice();
    }
}

[System.Serializable]
public class CombatResultDTO
{
    public int[] attackerDice;
    public int[] defenderDice;
    public int attackerLosses;
    public int defenderLosses;
    public bool captured;
    public int sourceId;
    public int targetId;
    public int sourceArmies;
    public int targetArmies;
}
