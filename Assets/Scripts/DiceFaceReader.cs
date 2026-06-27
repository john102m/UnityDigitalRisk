using UnityEngine;

public class DiceFaceReader : MonoBehaviour
{
    // Standard die: opposite faces sum to 7
    // Maps local direction to face value based on Unity cube orientation
    public int ReadTopFace()
    {
        // Check which local axis points most upward
        float dotUp = Vector3.Dot(transform.up, Vector3.up);
        float dotDown = Vector3.Dot(-transform.up, Vector3.up);
        float dotRight = Vector3.Dot(transform.right, Vector3.up);
        float dotLeft = Vector3.Dot(-transform.right, Vector3.up);
        float dotForward = Vector3.Dot(transform.forward, Vector3.up);
        float dotBack = Vector3.Dot(-transform.forward, Vector3.up);

        float max = dotUp;
        int face = 2;

        if (dotDown > max) { max = dotDown; face = 5; }
        if (dotRight > max) { max = dotRight; face = 3; }
        if (dotLeft > max) { max = dotLeft; face = 4; }
        if (dotForward > max) { max = dotForward; face = 1; }
        if (dotBack > max) { max = dotBack; face = 6; }

        return face;
    }
}
