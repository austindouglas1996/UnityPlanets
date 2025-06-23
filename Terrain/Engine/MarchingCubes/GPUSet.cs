using System.Collections.Generic;
using UnityEngine;

public class GPUSet
{
    public ComputeBuffer Triangle;
    public ComputeBuffer Args;
    public Bounds Bounds;

    public GPUSet(ComputeBuffer Triangle, ComputeBuffer Args, List<ChunkContext> contexts)
    {
        this.Triangle = Triangle;
        this.Args = Args;
        this.Bounds = this.ComputeBounds(contexts);
    }

    Bounds ComputeBounds(List<ChunkContext> chunkContexts)
    {
        if (chunkContexts.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = chunkContexts[0].WorldPosition;
        Vector3 max = chunkContexts[0].WorldPosition;

        foreach (var ctx in chunkContexts)
        {
            Vector3 pos = ctx.WorldPosition;
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