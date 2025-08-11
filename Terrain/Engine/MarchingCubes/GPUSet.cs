using System.Collections.Generic;
using UnityEngine;

public class GPUSet
{
    public ComputeBuffer Triangle;
    public ComputeBuffer Args;
    public Bounds Bounds;

    public GPUSet(ComputeBuffer Triangle, ComputeBuffer Args, List<ChunkKey> keys, IChunkServices services)
    {
        if (Args == null)
            throw new System.ArgumentNullException("args");

        this.Triangle = Triangle;
        this.Args = Args;
        this.Bounds = this.ComputeBounds(keys, services);
    }

    Bounds ComputeBounds(List<ChunkKey> chunkContexts, IChunkServices services)
    {
        if (chunkContexts.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = services.Layout.ToWorld(chunkContexts[0]);
        Vector3 max = services.Layout.ToWorld(chunkContexts[0]);

        foreach (var ctx in chunkContexts)
        {
            Vector3 pos = services.Layout.ToWorld(ctx);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = (max - min) + Vector3.one * 16;

        return new Bounds(center, size);
    }

    public void Dispose()
    {
        Args.Dispose();
        Triangle.Dispose();
        Args = null;
        Triangle = null;
    }
}