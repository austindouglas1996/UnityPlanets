using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkGenerationJob : IEqualityComparer<ChunkGenerationJob>
{
    public ChunkGenerationJob(ChunkContext context, CancellationTokenSource cts, ChunkModificationJob modificationJob = null)
    {
        Context = context;
        Completion = new TaskCompletionSource<ChunkData>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationSource = cts;
        ModificationJob = modificationJob;
    }

    public ChunkContext Context { get; private set; }

    public TaskCompletionSource<ChunkData> Completion;

    public ChunkModificationJob? ModificationJob { get; private set; }

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