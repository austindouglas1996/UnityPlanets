using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public readonly struct ChunkJobKey : IEquatable<ChunkJobKey>
{
    public readonly Vector3Int Coordinates;
    public readonly int LODIndex;

    public ChunkJobKey(Vector3Int coordinates, int lodIndex)
    {
        Coordinates = coordinates;
        LODIndex = lodIndex;
    }

    public bool Equals(ChunkJobKey other)
        => Coordinates.Equals(other.Coordinates) && LODIndex == other.LODIndex;

    public override int GetHashCode()
        => HashCode.Combine(Coordinates, LODIndex);

    public override bool Equals(object obj)
        => obj is ChunkJobKey other && Equals(other);
}


public class ChunkModificationJob
{
    public ChunkModificationJob(ChunkData existingData, TerrainBrush brush, bool isAdding)
    {
        ExistingData = existingData;
        Brush = brush;
        IsAdding = isAdding;
    }

    public ChunkData ExistingData { get; private set; }
    public TerrainBrush Brush { get; private set; }
    public bool IsAdding { get; private set; }
}

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