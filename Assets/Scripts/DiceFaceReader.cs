using UnityEngine;

/// <summary>
/// Determines which face of a die (Unity cube) is pointing upward.
/// Added to each die at spawn time by DiceRoller.
/// Uses dot product of each local axis against world-up to find the top face.
/// Standard die convention: opposite faces sum to 7 (1/6, 2/5, 3/4).
/// </summary>
public class DiceFaceReader : MonoBehaviour
{
    /// <summary>Returns the value (1–6) of the face currently pointing up.</summary>
    public int ReadTopFace()
    {
        float dotUp = Vector3.Dot(transform.up, Vector3.up);
        float dotDown = Vector3.Dot(-transform.up, Vector3.up);
        float dotRight = Vector3.Dot(transform.right, Vector3.up);
        float dotLeft = Vector3.Dot(-transform.right, Vector3.up);
        float dotForward = Vector3.Dot(transform.forward, Vector3.up);
        float dotBack = Vector3.Dot(-transform.forward, Vector3.up);

        Debug.Log($"[DiceFace] up={dotUp:F2} down={dotDown:F2} right={dotRight:F2} left={dotLeft:F2} fwd={dotForward:F2} back={dotBack:F2}");

        float max = dotUp;
        int face = 1; // local +Y = face 1

        if (dotDown > max) { max = dotDown; face = 6; }
        if (dotRight > max) { max = dotRight; face = 3; }
        if (dotLeft > max) { max = dotLeft; face = 4; }
        if (dotForward > max) { max = dotForward; face = 2; }
        if (dotBack > max) { max = dotBack; face = 5; }

        return face;
    }
}
