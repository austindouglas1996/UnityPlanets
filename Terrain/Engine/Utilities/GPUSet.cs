using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

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
        if (Args != null) Args.Dispose();
        if (Triangle != null) Triangle.Dispose();

        Args = null;
        Triangle = null;
    }

    static uint GetAppendCount(ComputeBuffer append)
    {
        using var raw = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(append, raw, 0);
        var u = new uint[1];
        raw.GetData(u);
        return u[0];
    }

    public static void ReadLod0TrianglesAsync(GPUSet set, System.Action<Triangle[]> onDone)
    {
        uint triCount = GetAppendCount(set.Triangle);
        if (triCount == 0) { onDone(System.Array.Empty<Triangle>()); return; }

        int stride = Marshal.SizeOf<Triangle>();     // 96 if using float4s
        int size = (int)(triCount * stride);

        AsyncGPUReadback.Request(set.Triangle, size, 0, req =>
        {
            if (req.hasError) { onDone(System.Array.Empty<Triangle>()); return; }
            onDone(req.GetData<Triangle>().ToArray());
        });
    }
}