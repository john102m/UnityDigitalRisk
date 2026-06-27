using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiceRoller : MonoBehaviour
{
    public GameObject dicePrefab;
    public Transform spawnPoint;
    public float throwForce = 5f;
    public float throwTorque = 10f;
    public float settleThreshold = 0.01f;
    public float settleTimeout = 4f;
    public Material attackerMaterial;
    public Material defenderMaterial;

    List<GameObject> activeDice = new();

    public IEnumerator RollDice(int[] attackerValues, int[] defenderValues, System.Action onComplete)
    {
        Debug.Log($"[DiceRoller] Rolling {attackerValues.Length} attacker + {defenderValues.Length} defender dice");
        ClearDice();

        // Spawn attacker dice (left side)
        for (int i = 0; i < attackerValues.Length; i++)
        {
            var die = SpawnDie(new Vector3(-1f + i * 0.8f, 3f, 0), attackerMaterial);
            activeDice.Add(die);
        }

        // Spawn defender dice (right side)
        for (int i = 0; i < defenderValues.Length; i++)
        {
            var die = SpawnDie(new Vector3(1.5f + i * 0.8f, 3f, 0), defenderMaterial);
            activeDice.Add(die);
        }

        Debug.Log($"[DiceRoller] Spawned {activeDice.Count} dice, waiting to settle");

        // Wait for all dice to settle
        yield return StartCoroutine(WaitForSettle());

        Debug.Log("[DiceRoller] Dice settled, correcting faces");

        // Force correct faces (rotate to match server values)
        int idx = 0;
        for (int i = 0; i < attackerValues.Length; i++)
            CorrectFace(activeDice[idx++], attackerValues[i]);
        for (int i = 0; i < defenderValues.Length; i++)
            CorrectFace(activeDice[idx++], defenderValues[i]);

        yield return new WaitForSeconds(1.5f);

        Debug.Log("[DiceRoller] Roll complete");
        onComplete?.Invoke();
    }

    GameObject SpawnDie(Vector3 localOffset, Material mat)
    {
        Vector3 pos = spawnPoint.position + localOffset;
        var die = Instantiate(dicePrefab, pos, Random.rotation);
        die.GetComponent<Renderer>().material = mat;
        die.AddComponent<DiceFaceReader>();

        var rb = die.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.down * throwForce;
        rb.angularVelocity = Random.insideUnitSphere * throwTorque;

        return die;
    }

    IEnumerator WaitForSettle()
    {
        yield return new WaitForSeconds(0.5f); // let physics start

        float elapsed = 0f;
        while (elapsed < settleTimeout)
        {
            bool allSettled = true;
            foreach (var die in activeDice)
            {
                var rb = die.GetComponent<Rigidbody>();
                if (rb.linearVelocity.magnitude > settleThreshold || rb.angularVelocity.magnitude > settleThreshold)
                {
                    allSettled = false;
                    break;
                }
            }
            if (allSettled) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void CorrectFace(GameObject die, int targetValue)
    {
        var reader = die.GetComponent<DiceFaceReader>();
        int current = reader.ReadTopFace();
        if (current == targetValue) return;

        // Rotate die so target face is on top
        var rb = die.GetComponent<Rigidbody>();
        rb.isKinematic = true;

        Quaternion correction = GetRotationForFace(targetValue);
        die.transform.rotation = correction;
    }

    Quaternion GetRotationForFace(int face)
    {
        return face switch
        {
            1 => Quaternion.Euler(90, 0, 0),
            2 => Quaternion.identity,
            3 => Quaternion.Euler(0, 0, -90),
            4 => Quaternion.Euler(0, 0, 90),
            5 => Quaternion.Euler(180, 0, 0),
            6 => Quaternion.Euler(-90, 0, 0),
            _ => Quaternion.identity
        };
    }

    public void ClearDice()
    {
        foreach (var die in activeDice)
            if (die != null) Destroy(die);
        activeDice.Clear();
    }
}
