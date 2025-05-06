using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkGenerationJob
{
    public ChunkGenerationJob(Vector3Int coordinates, int lODIndex, CancellationTokenSource cts)
    {
        Coordinates = coordinates;
        LODIndex = lODIndex;
        Completion = new TaskCompletionSource<ChunkData>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationSource = cts;
    }

    public Vector3Int Coordinates { get; private set; }
    public int LODIndex { get; private set; }

    public TaskCompletionSource<ChunkData> Completion;

    public CancellationTokenSource CancellationSource { get; private set; }
    public CancellationToken Token => CancellationSource.Token;

    public void Cancel()
    {
        if (!CancellationSource.IsCancellationRequested)
            CancellationSource.Cancel();
    }
}