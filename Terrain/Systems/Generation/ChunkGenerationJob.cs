using System;

public class ChunkGenerationJob
{
    public ChunkGenerationJob(ChunkContext context, Action<bool> action)
    {
        this.Context = context;
        this.OnDone = action;
    }

    public ChunkContext Context;
    public Action<bool> OnDone;
}