using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Orchestrates the dice roll visual sequence when combat occurs.
/// Handles two flows:
/// 1. CombatRollRequest — Unity rolls physics dice, reads faces, sends result to server
/// 2. CombatResult — received after server resolves (for state sync / non-Unity fallback)
/// </summary>
public class CombatTheatre : MonoBehaviour
{
    [Tooltip("The dice arena camera (perspective, renders to RenderTexture)")]
    public Camera diceCamera;

    [Tooltip("DiceRoller component that handles spawning and physics")]
    public DiceRoller diceRoller;

    [Tooltip("UI panel (RawImage) that displays the dice camera feed")]
    public GameObject dicePanelUI;

    [Tooltip("Camera flypath controller (Catmull-Rom spline)")]
    public CameraFlypath cameraFlypath;

    SignalRClient signalR;
    bool isPlaying;
    bool panelVisible;

    void Start()
    {
        signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnCombatRollRequest += OnCombatRollRequest;
        signalR.OnCombatResult += OnCombatResult;
        signalR.OnBlitzResult += OnBlitzResult;
        signalR.OnAttackSelection += OnAttackSelection;
        ShowDicePanel(false);

        GameStateManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnAttackSelection(int sourceId, int targetId)
    {
        // New attack target selected — clear old dice, keep panel hidden until roll
    }

    void OnStateChanged()
    {
        var state = GameStateManager.Instance.State;
        if (state == null) return;

        if (state.turnPhase != "Attack" && panelVisible && !isPlaying)
        {
            ShowDicePanel(false);
            panelVisible = false;
            diceRoller.ClearDice();
        }
    }

    /// <summary>Toggle dice panel visibility using CanvasGroup alpha.</summary>
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

    /// <summary>
    /// Server requests a dice roll from Unity — we ARE the dice engine.
    /// Spawn, simulate, read faces, send result back.
    /// </summary>
    void OnCombatRollRequest(int sourceId, int targetId, int attackerCount, int defenderCount)
    {
        Debug.Log($"[CombatTheatre] Roll request: {attackerCount}v{defenderCount} for {sourceId}→{targetId}");
        _ = PlayPhysicsRoll(sourceId, targetId, attackerCount, defenderCount);
    }

    /// <summary>
    /// CombatResult still arrives after server resolves — used for state sync.
    /// If we already showed the roll (via CombatRollRequest), just hide the panel on capture.
    /// If we didn't show it (fallback/blitz), ignore — web board handles display.
    /// </summary>
    void OnCombatResult(string json)
    {
        var result = JsonConvert.DeserializeObject<CombatResultDTO>(json);
        if (result == null) return;

        if (result.captured && panelVisible)
        {
            _ = HidePanelAfterDelay();
        }
    }

    async Awaitable HidePanelAfterDelay()
    {
        await Awaitable.WaitForSecondsAsync(4f);
        ShowDicePanel(false);
        panelVisible = false;
        diceRoller.ClearDice();
    }

    void OnBlitzResult(string json)
    {
        var result = JsonConvert.DeserializeObject<BlitzResultDTO>(json);
        if (result == null || result.finalAttackerDice == null || result.finalAttackerDice.Length == 0) return;

        _ = ShowBlitzDice(result);
    }

    async Awaitable ShowBlitzDice(BlitzResultDTO result)
    {
        ShowDicePanel(true);
        panelVisible = true;

        // Snap camera to result position looking at the dice
        if (cameraFlypath != null && cameraFlypath.resultPosition != null)
        {
            diceCamera.transform.position = cameraFlypath.resultPosition.position;
            if (cameraFlypath.lookTarget != null)
                diceCamera.transform.LookAt(cameraFlypath.lookTarget);
        }

        Vector3? centre = cameraFlypath != null && cameraFlypath.lookTarget != null
            ? cameraFlypath.lookTarget.position
            : null;
        diceRoller.PlaceDiceAtValues(result.finalAttackerDice, result.finalDefenderDice ?? new int[0], centre);
        await Awaitable.WaitForSecondsAsync(6f);
        ShowDicePanel(false);
        panelVisible = false;
        diceRoller.ClearDice();
    }

    /// <summary>Full physics-driven dice roll sequence.</summary>
    async Awaitable PlayPhysicsRoll(int sourceId, int targetId, int attackerCount, int defenderCount)
    {
        isPlaying = true;
        ShowDicePanel(true);
        panelVisible = true;

        // Fly camera along spline while dice roll
        CancellationTokenSource flyCts = null;
        if (cameraFlypath != null)
        {
            flyCts = new CancellationTokenSource();
            _ = cameraFlypath.Fly(diceCamera.transform, flyCts.Token);
        }

        // Roll dice and read naturally-landed faces
        var (attackerValues, defenderValues) = await diceRoller.RollAndRead(attackerCount, defenderCount);

        flyCts?.Cancel();

        // Send result back to server immediately, then hold for visual
        await signalR.SendDiceResult(sourceId, targetId, attackerValues, defenderValues);
        Debug.Log($"[CombatTheatre] Sent dice result to server");

        await Awaitable.WaitForSecondsAsync(3f);
        isPlaying = false;
    }
}

/// <summary>DTO matching server's CombatResult broadcast (camelCase JSON).</summary>
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

[System.Serializable]
public class BlitzResultDTO
{
    public int rounds;
    public bool captured;
    public int sourceId;
    public int targetId;
    public int[] finalAttackerDice;
    public int[] finalDefenderDice;
}
