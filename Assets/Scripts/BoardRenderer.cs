using UnityEngine;
using System.Collections;
using TMPro;

public class BoardRenderer : MonoBehaviour
{
    public GameObject tokenPrefab;
    public Transform mapTransform;
    public float tokenScale = 0.35f;
    public float textSize = 3f;
    public Color attackSourceGlow = Color.green;
    public Color attackTargetGlow = Color.red;
    public float glowDuration = 3f;
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.15f;

    GameObject[] tokens = new GameObject[42];
    Renderer[] tokenRenderers = new Renderer[42];
    TextMeshPro[] labels = new TextMeshPro[42];

    // Territory positions as % of map (from tv.html)
    static readonly Vector2[] COORDS = {
        new(8.1f,15.3f), new(16.9f,15.8f), new(35f,10.3f), new(16f,22.9f),
        new(22.3f,25.2f), new(29f,24.5f), new(16.5f,33.1f), new(23.3f,36.1f),
        new(16f,42.3f), new(23.7f,54.2f), new(30.9f,63f), new(25.9f,67.2f),
        new(26.4f,75.4f), new(42.9f,20.3f), new(49.6f,20.8f), new(42.5f,32.6f),
        new(49.9f,32.3f), new(59.2f,26f), new(42.5f,45.7f), new(52.4f,41.2f),
        new(45f,59.4f), new(53.5f,55.1f), new(57.7f,64.7f), new(54.3f,71.3f),
        new(63.6f,84f), new(54.9f,83.4f), new(68.8f,22.5f), new(74.4f,18.3f),
        new(66.9f,35.4f), new(80.8f,13.8f), new(61f,50.9f), new(80.4f,24.8f),
        new(72.8f,50.2f), new(78.1f,42.6f), new(80.6f,34.6f), new(80.3f,54.1f),
        new(87.8f,13.9f), new(91.1f,35.6f), new(82.2f,69.4f), new(90.5f,66.7f),
        new(86.3f,82.9f), new(94.8f,83.5f)
    };

    void Start()
    {
        GameStateManager.Instance.OnStateChanged += Refresh;
        var signalR = FindAnyObjectByType<SignalRClient>();
        signalR.OnAttackSelection += OnAttackSelection;
        signalR.OnCombatResult += json => { };
        signalR.OnBlitzResult += json => { };
        SpawnTokens();
    }

    int glowSourceId = -1;
    int glowTargetId = -1;
    string lastTurnPhase = "";
    Coroutine pulseCoroutine;

    void OnAttackSelection(int sourceId, int targetId)
    {
        ClearGlow();
        glowSourceId = sourceId;
        glowTargetId = targetId;

        if (sourceId >= 0 && sourceId < 42 && tokenRenderers[sourceId] != null)
            tokenRenderers[sourceId].material.SetColor("_EmissionColor", attackSourceGlow * 2f);
        if (targetId >= 0 && targetId < 42 && tokenRenderers[targetId] != null)
            tokenRenderers[targetId].material.SetColor("_EmissionColor", attackTargetGlow * 2f);

        pulseCoroutine = StartCoroutine(PulseTokens());
    }

    IEnumerator PulseTokens()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * pulseSpeed;
            float scale = 1f + Mathf.Sin(t) * pulseAmount;
            var pulseScale = tokenPrefab.transform.localScale * tokenScale * scale;

            if (glowSourceId >= 0 && glowSourceId < 42 && tokens[glowSourceId] != null)
                tokens[glowSourceId].transform.localScale = pulseScale;
            if (glowTargetId >= 0 && glowTargetId < 42 && tokens[glowTargetId] != null)
                tokens[glowTargetId].transform.localScale = pulseScale;

            yield return null;
        }
    }

    void ClearGlow()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }

        var normalScale = tokenPrefab.transform.localScale * tokenScale;
        if (glowSourceId >= 0 && glowSourceId < 42 && tokens[glowSourceId] != null)
            tokens[glowSourceId].transform.localScale = normalScale;
        if (glowTargetId >= 0 && glowTargetId < 42 && tokens[glowTargetId] != null)
            tokens[glowTargetId].transform.localScale = normalScale;

        if (glowSourceId >= 0 && glowSourceId < 42 && tokenRenderers[glowSourceId] != null)
            tokenRenderers[glowSourceId].material.SetColor("_EmissionColor", Color.black);
        if (glowTargetId >= 0 && glowTargetId < 42 && tokenRenderers[glowTargetId] != null)
            tokenRenderers[glowTargetId].material.SetColor("_EmissionColor", Color.black);
        glowSourceId = -1;
        glowTargetId = -1;
    }

    void SpawnTokens()
    {
        var bounds = mapTransform.GetComponent<Renderer>().bounds;

        for (int i = 0; i < 42; i++)
        {
            float x = bounds.min.x + (COORDS[i].x / 100f) * bounds.size.x;
            float y = bounds.max.y - (COORDS[i].y / 100f) * bounds.size.y;

            // Spawn token
            var token = Instantiate(tokenPrefab, new Vector3(x, y, -0.1f), tokenPrefab.transform.rotation, transform);
            token.name = $"Territory_{i}";
            token.transform.localScale = tokenPrefab.transform.localScale * tokenScale;
            tokens[i] = token;
            tokenRenderers[i] = token.GetComponent<Renderer>();
            tokenRenderers[i].material.EnableKeyword("_EMISSION");

            // Spawn text label as child of token (sits on top)
            var textGO = new GameObject($"Label_{i}");
            textGO.transform.SetParent(token.transform);
            textGO.transform.localPosition = new Vector3(0, 1.1f, 0);
            textGO.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tmp = textGO.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.fontSize = textSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(2, 1);
            labels[i] = tmp;
        }
    }

    void Refresh()
    {
        var state = GameStateManager.Instance.State;
        if (state?.territories == null) return;

        if (state.turnPhase != lastTurnPhase)
        {
            ClearGlow();
            lastTurnPhase = state.turnPhase;
        }

        foreach (var t in state.territories)
        {
            if (t.id < 0 || t.id >= 42) continue;

            if (labels[t.id] != null)
                labels[t.id].text = t.armies.ToString();

            if (tokenRenderers[t.id] != null && t.ownerId >= 0 && t.ownerId < state.players.Count)
            {
                var player = state.players[t.ownerId];
                if (ColorUtility.TryParseHtmlString(player.colour, out Color c))
                    tokenRenderers[t.id].material.color = c;
            }
        }
    }
}
