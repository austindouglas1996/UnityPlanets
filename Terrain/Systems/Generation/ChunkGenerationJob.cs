using System;
using UnityEngine.InputSystem;

public class ChunkGenerationJob
{
    public ChunkGenerationJob(ChunkKey key, Action<bool> action)
    {
        this.Key = key;
        this.OnDone = action;
    }

    public ChunkKey Key;
    public Action<bool> OnDone;
}