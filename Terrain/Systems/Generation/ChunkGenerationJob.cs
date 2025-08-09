using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkGenerationJob : IEqualityComparer<ChunkGenerationJob>
{
    public ChunkGenerationJob(ChunkContext context, CancellationTokenSource cts)
    {
        Context = context;
        Completion = new TaskCompletionSource<ChunkContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationSource = cts;
    }

    public ChunkContext Context { get; private set; }

    public TaskCompletionSource<ChunkContext> Completion;

    public CancellationTokenSource CancellationSource { get; private set; }
    public CancellationToken Token => CancellationSource.Token;

    public void Cancel()
    {
        if (!CancellationSource.IsCancellationRequested)
            CancellationSource.Cancel();
    }

    public bool Equals(ChunkGenerationJob x, ChunkGenerationJob y)
    {
        if (x == null || y == null)
            return false;

        return x.Context.Coordinates == y.Context.Coordinates &&
            x.Context.LODIndex == y.Context.LODIndex;
    }

    public int GetHashCode(ChunkGenerationJob obj)
    {
        return HashCode.Combine(obj.Context.Coordinates, obj.Context.LODIndex);
    }
}