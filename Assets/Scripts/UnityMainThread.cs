using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marshals actions from background threads onto Unity's main thread.
/// SignalR callbacks arrive on thread pool threads — Unity APIs (GameObjects, Transforms, etc.)
/// can only be accessed from the main thread. Enqueue work here, it runs next Update().
/// </summary>
public class UnityMainThread : MonoBehaviour
{
    static readonly Queue<Action> queue = new();
    static UnityMainThread instance;

    void Awake()
    {
        // Singleton — survives scene loads
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);

        // Keep Unity running when editor/app loses focus (e.g. switching to browser/VS)
        Application.runInBackground = true;
    }

    /// <summary>Processes all queued actions each frame on the main thread.</summary>
    void Update()
    {
        lock (queue)
        {
            while (queue.Count > 0)
                queue.Dequeue()?.Invoke();
        }
    }

    /// <summary>Queue an action to run on the main thread next frame.</summary>
    public static void Enqueue(Action action)
    {
        lock (queue) { queue.Enqueue(action); }
    }
}
