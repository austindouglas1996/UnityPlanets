using System.Collections.Generic;
using UnityEngine;

public class ChunkRegionRenderDataGPU
{
    public ComputeBuffer Triangle;
    public ComputeBuffer Args;
    public Bounds Bounds;
    private IChunkLayout Layout;

    public ChunkRegionRenderDataGPU(IChunkLayout layout, ComputeBuffer Triangle, ComputeBuffer Args, List<ChunkKey> keys)
    {
        this.Layout = layout;
        this.Triangle = Triangle;
        this.Args = Args;
        this.Bounds = this.ComputeBounds(keys);
    }

    public void Dispose()
    {
        Args.Dispose();
        Triangle.Dispose();
    }

    public void Draw(Material mat)
    {
        if (Triangle == null) return;

        mat.SetBuffer("_TriangleBuffer", Triangle);
        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, Args, 0);
    }

    private Bounds ComputeBounds(List<ChunkKey> chunkContexts)
    {
        if (chunkContexts.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = this.Layout.ToWorld(chunkContexts[0]);
        Vector3 max = min;

        foreach (var ctx in chunkContexts)
        {
            Vector3 pos = this.Layout.ToWorld(ctx);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = (max - min) + Vector3.one * 16;

        return new Bounds(center, size);
    }
}