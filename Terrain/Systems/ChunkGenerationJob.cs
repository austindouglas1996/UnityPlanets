using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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

public class ChunkGenerationJob
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
}