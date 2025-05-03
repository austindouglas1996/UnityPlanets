using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChunkJob
{
    public ChunkJob(Vector3Int coordinates, int lODIndex, Task task, CancellationTokenSource cts)
    {
        Coordinates = coordinates;
        LODIndex = lODIndex;
        Task = task;
        CancellationSource = cts;
    }

    public Vector3Int Coordinates { get; private set; }
    public int LODIndex { get; private set; }
    public Task Task { get; private set; }

    public CancellationTokenSource CancellationSource { get; private set; }
    public CancellationToken Token => CancellationSource.Token;

    public void Cancel()
    {
        if (!CancellationSource.IsCancellationRequested)
            CancellationSource.Cancel();
    }
}