using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// State machine orchestrating the dice roll visual sequence on the Unity TV board.
/// All transitions are explicit — each state has defined entry conditions and only
/// responds to events valid for that state. Invalid events are ignored.
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

    [Header("Panel Positioning")]
    [Tooltip("Panel X when action is on the right (panel goes left)")]
    public float panelXLeft = -600f;

    [Tooltip("Panel X when action spans both sides (panel centred)")]
    public float panelXCentre = 0f;

    [Tooltip("Panel X when action is on the left (panel goes right)")]
    public float panelXRight = 400f;

    // ─── State ───────────────────────────────────────────────────────────────
    enum CombatState
    {
        Idle,               // No panel, no dice, waiting for next combat
        WaitingForDice,     // Panel shown, camera flying, attacker dice spawned, awaiting defender
        Settling,           // Both sets spawned, physics running
        ShowingResult,      // Dice settled, holding for players to read
        Hiding,             // Delayed hide after capture (4s countdown)
        ShowingBlitz        // Blitz final dice on display
    }

    CombatState state = CombatState.Idle;
    bool cameraFlownThisTurn;
    CancellationTokenSource hideCts;
    SignalRClient signalR;
    RectTransform panelRect;
    int currentSourceId = -1;
    int currentTargetId = -1;
    BoardCamera boardCamera;
    BoardRenderer boardRenderer;

    // ─── Setup ───────────────────────────────────────────────────────────────
    void Start()
    {
        panelRect = dicePanelUI.GetComponent<RectTransform>();
        boardCamera = FindAnyObjectByType<BoardCamera>();
        boardRenderer = FindAnyObjectByType<BoardRenderer>();
        signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnCombatRollRequest += OnCombatRollRequest;
        signalR.OnSpawnDice += OnSpawnDice;
        signalR.OnCombatResult += OnCombatResult;
        signalR.OnBlitzResult += OnBlitzResult;
        signalR.OnAttackSelection += OnAttackSelection;
        ShowPanel(false);

        GameStateManager.Instance.OnStateChanged += OnStateChanged;
    }

    // ─── Events ──────────────────────────────────────────────────────────────

    /// <summary>Server tells Unity to spawn one player's dice (player-rolled flow).</summary>
    void OnSpawnDice(string role, int diceCount)
    {
        Debug.Log($"[Combat] SpawnDice: {role} x{diceCount} (state={state})");

        if (role == "attacker")
        {
            // Attacker always starts a new combat — enter WaitingForDice
            EnterWaitingForDice();
            diceRoller.SpawnSet(role, diceCount);
        }
        else if (role == "defender")
        {
            // Defender dice arrive — transition to Settling
            diceRoller.SpawnSet(role, diceCount);
            EnterSettling();
        }
    }

    /// <summary>Legacy: server requests both dice at once (used by /admin/testdice).</summary>
    void OnCombatRollRequest(int sourceId, int targetId, int attackerCount, int defenderCount)
    {
        Debug.Log($"[Combat] CombatRollRequest: {attackerCount}v{defenderCount}");
        _ = PlayFullRoll(sourceId, targetId, attackerCount, defenderCount);
    }

    /// <summary>Server resolved combat — hide panel on capture, ignore if stale.</summary>
    void OnCombatResult(string json)
    {
        var result = JsonConvert.DeserializeObject<CombatResultDTO>(json);
        if (result == null) return;

        // If we're waiting for dice but server resolved without us (timeout/fallback),
        // dismiss the arena — the combat is over.
        if (state == CombatState.WaitingForDice)
        {
            ShowPanel(false);
            diceRoller.ClearDice();
            cameraFlownThisTurn = false;
            TransitionTo(CombatState.Idle);
            return;
        }

        if (result.captured)
            EnterHiding();
    }

    /// <summary>Blitz completed server-side — show final dice with camera sweep.</summary>
    void OnBlitzResult(string json)
    {
        var result = JsonConvert.DeserializeObject<BlitzResultDTO>(json);
        if (result?.finalAttackerDice == null || result.finalAttackerDice.Length == 0) return;

        _ = ShowBlitzDice(result);
    }

    void OnAttackSelection(int sourceId, int targetId)
    {
        currentSourceId = sourceId;
        currentTargetId = targetId;
        ZoomIn();
    }

    void OnStateChanged()
    {
        var gs = GameStateManager.Instance.State;
        if (gs == null) return;

        if (gs.turnPhase != "Attack")
        {
            cameraFlownThisTurn = false;
            if (state == CombatState.Idle || state == CombatState.ShowingResult)
            {
                ShowPanel(false);
                diceRoller.ClearDice();
                ZoomOut();
                TransitionTo(CombatState.Idle);
            }
        }
    }

    // ─── State Transitions ───────────────────────────────────────────────────

    void EnterWaitingForDice()
    {
        hideCts?.Cancel();
        diceRoller.ClearDice();
        PositionPanel(currentSourceId, currentTargetId);
        ShowPanel(true);
        TransitionTo(CombatState.WaitingForDice);

        if (!cameraFlownThisTurn && cameraFlypath != null)
        {
            cameraFlownThisTurn = true;
            var cts = new CancellationTokenSource();
            _ = cameraFlypath.Fly(diceCamera.transform, cts.Token);
        }
    }

    void EnterSettling()
    {
        TransitionTo(CombatState.Settling);
        _ = WaitSettleAndSend();
    }

    void EnterHiding()
    {
        TransitionTo(CombatState.Hiding);
        hideCts?.Cancel();
        hideCts = new CancellationTokenSource();
        _ = HideAfterDelay(hideCts.Token);
    }

    void TransitionTo(CombatState newState)
    {
        Debug.Log($"[Combat] {state} → {newState}");
        state = newState;
    }

    // ─── Async Sequences ─────────────────────────────────────────────────────

    async Awaitable WaitSettleAndSend()
    {
        var (attackerValues, defenderValues) = await diceRoller.WaitAndReadAll();
        await signalR.SendDiceResult(0, 0, attackerValues, defenderValues);
        Debug.Log($"[Combat] Sent result: A[{string.Join(",", attackerValues)}] D[{string.Join(",", defenderValues)}]");

        TransitionTo(CombatState.ShowingResult);
        await Awaitable.WaitForSecondsAsync(3f);

        // After hold, if still showing result (no new combat started), go idle
        if (state == CombatState.ShowingResult)
        {
            TransitionTo(CombatState.Idle);
        }
    }

    async Awaitable HideAfterDelay(CancellationToken token)
    {
        await Awaitable.WaitForSecondsAsync(4f);
        if (token.IsCancellationRequested) return;

        ShowPanel(false);
        cameraFlownThisTurn = false;
        diceRoller.ClearDice();
        ZoomOut();
        TransitionTo(CombatState.Idle);
    }

    async Awaitable ShowBlitzDice(BlitzResultDTO result)
    {
        TransitionTo(CombatState.ShowingBlitz);
        ShowPanel(true);
        diceRoller.ClearDice();

        // Camera sweep
        if (cameraFlypath != null)
        {
            var cts = new CancellationTokenSource();
            _ = cameraFlypath.Fly(diceCamera.transform, cts.Token);
            await Awaitable.WaitForSecondsAsync(cameraFlypath.duration + 0.5f);
            cts.Cancel();
            if (state != CombatState.ShowingBlitz) return; // new combat started
        }

        // Place final dice
        Vector3? centre = cameraFlypath != null && cameraFlypath.lookTarget != null
            ? cameraFlypath.lookTarget.position : null;
        diceRoller.PlaceDiceAtValues(result.finalAttackerDice, result.finalDefenderDice ?? new int[0], centre);
        await Awaitable.WaitForSecondsAsync(6f);

        if (state != CombatState.ShowingBlitz) return; // new combat started during hold

        ShowPanel(false);
        diceRoller.ClearDice();
        TransitionTo(CombatState.Idle);
    }

    /// <summary>Legacy full roll (both sets at once) — used by /admin/testdice.</summary>
    async Awaitable PlayFullRoll(int sourceId, int targetId, int attackerCount, int defenderCount)
    {
        EnterWaitingForDice();

        CancellationTokenSource flyCts = null;
        if (cameraFlypath != null)
        {
            flyCts = new CancellationTokenSource();
            _ = cameraFlypath.Fly(diceCamera.transform, flyCts.Token);
        }

        var (attackerValues, defenderValues) = await diceRoller.RollAndRead(attackerCount, defenderCount);
        flyCts?.Cancel();

        await signalR.SendDiceResult(sourceId, targetId, attackerValues, defenderValues);
        TransitionTo(CombatState.ShowingResult);
        await Awaitable.WaitForSecondsAsync(3f);

        if (state == CombatState.ShowingResult)
            TransitionTo(CombatState.Idle);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    void PositionPanel(int sourceId, int targetId)
    {
        float sourceX = BoardRenderer.GetTerritoryX(sourceId);
        float targetX = BoardRenderer.GetTerritoryX(targetId);

        // Average X of both territories — put panel on the opposite side
        float avgX = (sourceX + targetX) / 2f;

        // Special case: Kamchatka(36) vs Alaska(0) — huge X gap means wrap-around
        float gap = Mathf.Abs(sourceX - targetX);
        float x;
        if (gap > 50f)
            x = panelXCentre;      // wrap-around (e.g. Kamchatka vs Alaska)
        else if (avgX < 50f)
            x = panelXRight;       // action on left → panel right
        else
            x = panelXLeft;        // action on right → panel left

        var pos = panelRect.anchoredPosition;
        pos.x = x;
        panelRect.anchoredPosition = pos;
    }

    void ZoomIn()
    {
        if (boardCamera == null || boardRenderer == null) return;
        if (currentSourceId < 0 || currentTargetId < 0) return;
        var source = boardRenderer.GetTerritoryWorldPosition(currentSourceId);
        var target = boardRenderer.GetTerritoryWorldPosition(currentTargetId);

        // Offset the zoom away from the dice panel so action stays visible
        float sourceX = BoardRenderer.GetTerritoryX(currentSourceId);
        float targetX = BoardRenderer.GetTerritoryX(currentTargetId);
        float avgX = (sourceX + targetX) / 2f;
        float gap = Mathf.Abs(sourceX - targetX);

        // Determine which side the panel is on and bias the opposite way
        float biasX = 0f;
        if (gap <= 50f) // not the wrap-around case
        {
            if (avgX >= 50f)
                biasX = 1f;   // panel is left, shift camera right so action shows right of centre
            else
                biasX = -1f;  // panel is right, shift camera left so action shows left of centre
        }

        boardCamera.ZoomToAction(source, target, biasX);
    }

    void ZoomOut()
    {
        boardCamera?.ZoomOut();
    }

    void ShowPanel(bool show)
    {
        if (diceCamera != null) diceCamera.enabled = show;
        if (dicePanelUI != null)
        {
            var cg = dicePanelUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = dicePanelUI.AddComponent<CanvasGroup>();
            cg.alpha = show ? 1f : 0f;
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

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
