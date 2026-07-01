using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns dice into the arena, applies physics forces, waits for them to settle,
/// then reads the naturally-landed faces. No correction — physics determines the result.
/// </summary>
public class DiceRoller : MonoBehaviour
{
    [Tooltip("Die prefab — cube with Rigidbody + BoxCollider + PhysicsMaterial")]
    public GameObject dicePrefab;

    [Tooltip("Position above the arena where dice spawn")]
    public Transform spawnPoint;

    [Tooltip("Downward velocity applied to dice on spawn")]
    public float throwForce = 8f;

    [Tooltip("Random angular velocity applied to dice (higher = more spin)")]
    public float throwTorque = 10f;

    [Tooltip("Velocity below this = die has stopped moving")]
    public float settleThreshold = 0.1f;

    [Tooltip("Max seconds to wait for dice to settle before forcing")]
    public float settleTimeout = 4f;

    [Tooltip("Material for attacker dice (red)")]
    public Material attackerMaterial;

    [Tooltip("Material for defender dice (white)")]
    public Material defenderMaterial;

    List<GameObject> activeDice = new();
    int attackerDiceCount;

    /// <summary>
    /// Roll dice using real physics and read the result. No face correction.
    /// Returns the naturally-landed face values for attacker and defender dice.
    /// Used by legacy CombatRollRequest flow (both sets at once).
    /// </summary>
    public async Awaitable<(int[] attackerValues, int[] defenderValues)> RollAndRead(int attackerCount, int defenderCount)
    {
        ClearDice();
        attackerDiceCount = attackerCount;
        SpawnSet("attacker", attackerCount);
        SpawnSet("defender", defenderCount);
        await WaitForSettle();
        return ReadAll();
    }

    /// <summary>Spawn one player's dice into the arena. First call clears previous dice.</summary>
    public void SpawnSet(string role, int count)
    {
        bool isAttacker = role == "attacker";
        Material mat = isAttacker ? attackerMaterial : defenderMaterial;
        float xBase = isAttacker ? -1f : 1.5f;

        if (isAttacker) attackerDiceCount = count;

        DiceSound.Arm();

        for (int i = 0; i < count; i++)
            activeDice.Add(SpawnDie(new Vector3(xBase + i * 0.8f, 3f, 0), mat));

        Debug.Log($"[DiceRoller] Spawned {count} {role} dice");
    }

    /// <summary>Wait for all dice to settle then read faces.</summary>
    public async Awaitable<(int[] attackerValues, int[] defenderValues)> WaitAndReadAll()
    {
        await WaitForSettle();
        return ReadAll();
    }

    (int[] attackerValues, int[] defenderValues) ReadAll()
    {
        int defenderCount = activeDice.Count - attackerDiceCount;
        var attackerValues = new int[attackerDiceCount];
        var defenderValues = new int[defenderCount];
        int idx = 0;
        for (int i = 0; i < attackerDiceCount; i++)
            attackerValues[i] = activeDice[idx++].GetComponent<DiceFaceReader>().ReadTopFace();
        for (int i = 0; i < defenderCount; i++)
            defenderValues[i] = activeDice[idx++].GetComponent<DiceFaceReader>().ReadTopFace();

        Debug.Log($"[DiceRoller] Read faces — attacker: [{string.Join(",", attackerValues)}], defender: [{string.Join(",", defenderValues)}]");
        return (attackerValues, defenderValues);
    }

    /// <summary>Instantiate a single die with random rotation and applied forces.</summary>
    GameObject SpawnDie(Vector3 localOffset, Material mat)
    {
        Vector3 pos = spawnPoint.position + localOffset;
        var die = Instantiate(dicePrefab, pos, Random.rotation);
        die.AddComponent<DiceFaceReader>();

        // Set layer and material on all children (FBX models nest the mesh)
        foreach (var r in die.GetComponentsInChildren<Renderer>())
        {
            r.gameObject.layer = LayerMask.NameToLayer("DiceArena");
            r.material = mat;
        }
        die.layer = LayerMask.NameToLayer("DiceArena");

        var rb = die.GetComponent<Rigidbody>();
        Vector3 throwDir = new Vector3(
            Random.Range(-0.3f, 0.3f),
            Random.Range(-0.5f, 0.2f),
            Random.Range(0.8f, 1.5f)
        ).normalized;

        rb.linearVelocity = throwDir * throwForce;
        rb.angularVelocity = Random.insideUnitSphere * throwTorque;

        return die;
    }

    /// <summary>
    /// Wait each frame until all dice velocities drop below threshold, or timeout expires.
    /// </summary>
    async Awaitable WaitForSettle()
    {
        await Awaitable.NextFrameAsync();

        float elapsed = 0f;
        while (elapsed < settleTimeout)
        {
            bool allSettled = true;
            foreach (var die in activeDice)
            {
                var rb = die.GetComponent<Rigidbody>();
                if (!IsSettled(rb))
                {
                    allSettled = false;
                    break;
                }
            }
            if (allSettled) return;
            elapsed += Time.deltaTime;
            await Awaitable.NextFrameAsync();
        }
    }

    bool IsSettled(Rigidbody rb)
    {
        return rb.linearVelocity.magnitude < settleThreshold
            && rb.angularVelocity.magnitude < settleThreshold;
    }

    /// <summary>Destroy all spawned dice (called after sequence completes).</summary>
    public void ClearDice()
    {
        foreach (var die in activeDice)
            if (die != null) Destroy(die);
        activeDice.Clear();
    }

    /// <summary>Place dice showing specific face values scattered around a centre point.</summary>
    public void PlaceDiceAtValues(int[] attackerValues, int[] defenderValues, Vector3? centre = null)
    {
        ClearDice();

        Vector3 c = centre ?? spawnPoint.position + new Vector3(0f, 0.5f, 2f);
        c.y -= 0.6f;
        float spread = 1.2f;

        for (int i = 0; i < attackerValues.Length; i++)
        {
            var offset = new Vector3(-spread - i * 1.0f, 0f, Random.Range(-0.4f, 0.4f));
            var rot = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * GetRotationForFace(attackerValues[i]);
            var die = Instantiate(dicePrefab, c + offset, rot);
            SetupPlacedDie(die, attackerMaterial);
        }

        for (int i = 0; i < defenderValues.Length; i++)
        {
            var offset = new Vector3(spread + i * 1.0f, 0f, Random.Range(-0.4f, 0.4f));
            var rot = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * GetRotationForFace(defenderValues[i]);
            var die = Instantiate(dicePrefab, c + offset, rot);
            SetupPlacedDie(die, defenderMaterial);
        }
    }

    void SetupPlacedDie(GameObject die, Material mat)
    {
        foreach (var r in die.GetComponentsInChildren<Renderer>())
        {
            r.gameObject.layer = LayerMask.NameToLayer("DiceArena");
            r.material = mat;
        }
        die.layer = LayerMask.NameToLayer("DiceArena");
        var rb = die.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        activeDice.Add(die);
    }

    /// <summary>Returns a rotation that places the given face value on top (matching FBX mapping).</summary>
    static Quaternion GetRotationForFace(int face)
    {
        return face switch
        {
            1 => Quaternion.identity,                           // +Y up
            6 => Quaternion.Euler(0f, 0f, 180f),               // -Y up
            3 => Quaternion.Euler(0f, 0f, -90f),               // +X up
            4 => Quaternion.Euler(0f, 0f, 90f),                // -X up
            2 => Quaternion.Euler(90f, 0f, 0f),                // +Z up
            5 => Quaternion.Euler(-90f, 0f, 0f),               // -Z up
            _ => Quaternion.identity
        };
    }
}
