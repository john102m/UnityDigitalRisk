using UnityEngine;

/// <summary>
/// Smoothly zooms the main camera toward attacking/defending territories,
/// then zooms back out when combat resolves. Attach to Main Camera.
/// </summary>
public class BoardCamera : MonoBehaviour
{
    [Tooltip("Orthographic size when zoomed in on combat")]
    public float zoomInSize = 2.5f;

    [Tooltip("How fast the camera zooms (higher = snappier)")]
    public float zoomSpeed = 3f;

    [Tooltip("How far to offset camera away from dice panel (world units)")]
    public float panelBiasOffset = 1.5f;

    float defaultSize;
    Vector3 defaultPosition;
    Vector3 targetPosition;
    float targetSize;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultSize = cam.orthographicSize;
        defaultPosition = transform.position;
        targetSize = defaultSize;
        targetPosition = defaultPosition;
    }

    void Update()
    {
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * zoomSpeed);
    }

    /// <summary>Zoom in to the midpoint between source and target, biased away from dice panel.</summary>
    /// <param name="biasX">-1 = shift left, 0 = centre, 1 = shift right</param>
    public void ZoomToAction(Vector3 worldPosSource, Vector3 worldPosTarget, float biasX = 0f)
    {
        Vector3 midpoint = (worldPosSource + worldPosTarget) / 2f;
        midpoint.x += biasX * panelBiasOffset;
        targetPosition = new Vector3(midpoint.x, midpoint.y, defaultPosition.z);
        targetSize = zoomInSize;
    }

    /// <summary>Zoom back out to default overview.</summary>
    public void ZoomOut()
    {
        targetPosition = defaultPosition;
        targetSize = defaultSize;
    }
}
