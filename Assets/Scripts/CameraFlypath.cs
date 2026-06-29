using System.Threading;
using UnityEngine;

/// <summary>
/// Flies a camera along a Catmull-Rom spline defined by waypoints.
/// Same maths as Three.js CatmullRomCurve3 — path passes through all points.
/// </summary>
public class CameraFlypath : MonoBehaviour
{
    [Tooltip("Ordered waypoints — camera passes through each one (minimum 4)")]
    public Transform[] waypoints;

    [Tooltip("Camera looks at this during flight (e.g. centre of arena floor)")]
    public Transform lookTarget;

    [Tooltip("Final resting position for camera after fly (overhead result view)")]
    public Transform resultPosition;

    [Tooltip("Total flight duration in seconds")]
    public float duration = 2.5f;

    /// <summary>Fly the camera along the spline with per-roll randomisation. Cancels cleanly via token.</summary>
    public async Awaitable Fly(Transform cam, CancellationToken ct)
    {
        int count = waypoints.Length;
        if (count < 4) return;

        // Randomise each roll: position jitter, speed, and direction
        Vector3 jitter = new Vector3(
            Random.Range(-1.5f, 1.5f),
            Random.Range(-0.5f, 0.5f),
            Random.Range(-1f, 1f)
        );
        float speedVariation = Random.Range(0.8f, 1.3f);
        bool reverse = Random.value > 0.5f;

        float elapsed = 0f;
        float actualDuration = duration * speedVariation;

        while (elapsed < actualDuration && !ct.IsCancellationRequested)
        {
            float t = elapsed / actualDuration;
            t = t * t * (3f - 2f * t); // smoothstep ease-in-out

            if (reverse) t = 1f - t;

            float scaled = t * (count - 3);
            int seg = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, count - 4);
            float segT = scaled - seg;

            Vector3 pos = CatmullRom(
                waypoints[seg].position,
                waypoints[seg + 1].position,
                waypoints[seg + 2].position,
                waypoints[seg + 3].position,
                segT);

            cam.position = pos + jitter * (1f - t); // jitter fades out toward end

            if (lookTarget != null)
                cam.LookAt(lookTarget);

            elapsed += Time.deltaTime;
            await Awaitable.NextFrameAsync();
        }

        // Smooth transition to a random point around the result position (circle radius)
        if (resultPosition != null)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 finalPos = resultPosition.position + offset;

            Vector3 startPos = cam.position;
            Quaternion startRot = cam.rotation;
            Quaternion endRot = Quaternion.LookRotation(lookTarget.position - finalPos);
            float transitionTime = 0.8f;
            float te = 0f;
            while (te < transitionTime)
            {
                float s = te / transitionTime;
                s = s * s * (3f - 2f * s); // smoothstep
                cam.position = Vector3.Lerp(startPos, finalPos, s);
                cam.rotation = Quaternion.Slerp(startRot, endRot, s);
                te += Time.deltaTime;
                await Awaitable.NextFrameAsync();
            }
            cam.position = finalPos;
            if (lookTarget != null)
                cam.LookAt(lookTarget);
        }
    }

    /// <summary>Catmull-Rom interpolation — passes through p1 toward p2.</summary>
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>Draw the spline path and waypoints in Scene view.</summary>
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 4) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
            if (waypoints[i] != null)
                Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);

        Gizmos.color = Color.cyan;
        Vector3 prev = waypoints[1].position;
        for (float t = 0.05f; t <= 1f; t += 0.05f)
        {
            float scaled = t * (waypoints.Length - 3);
            int seg = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, waypoints.Length - 4);
            float segT = scaled - seg;
            Vector3 point = CatmullRom(
                waypoints[seg].position, waypoints[seg + 1].position,
                waypoints[seg + 2].position, waypoints[seg + 3].position, segT);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
    }
}
