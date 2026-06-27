using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThread : MonoBehaviour
{
    static readonly Queue<Action> queue = new();
    static UnityMainThread instance;

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
        Application.runInBackground = true;
    }

    void Update()
    {
        lock (queue)
        {
            while (queue.Count > 0)
                queue.Dequeue()?.Invoke();
        }
    }

    public static void Enqueue(Action action)
    {
        lock (queue) { queue.Enqueue(action); }
    }
}
