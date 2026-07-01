using UnityEngine;

/// <summary>
/// Attach to die prefab. First die to hit the floor plays the rattle once.
/// All other dice stay silent. Resets on each new spawn via DiceRoller.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DiceSound : MonoBehaviour
{
    [Tooltip("The dice rattle clip")]
    public AudioClip rattle;

    [Tooltip("Below this Y position, collision can trigger sound")]
    public float soundBelowY = 1f;

    static bool hasPlayedThisThrow;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasPlayedThisThrow) return;
        if (rattle == null) return;
        if (transform.position.y > soundBelowY) return;

        audioSource.PlayOneShot(rattle);
        hasPlayedThisThrow = true;
    }

    /// <summary>Call before each spawn to re-arm the sound.</summary>
    public static void Arm()
    {
        hasPlayedThisThrow = false;
    }
}
