using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draw-ready container for a chunk group: triangle append buffer + indirect args + culling bounds.
/// Created by generation, consumed by rendering and (optionally) collider baking.
/// </summary>
public class ChunkRenderBatch : IDisposable
{
    /// <summary>
    /// Append buffer containing generated triangles (ComputeBufferType.Append).
    /// </summary>
    public ComputeBuffer Triangle;

    /// <summary>
    /// Indirect draw arguments buffer (ComputeBufferType.IndirectArguments).
    /// </summary>
    public ComputeBuffer Args;

    /// <summary>
    /// World-space bounds for this batch used for culling when using non-Now draw paths.
    /// </summary>
    public Bounds Bounds;

    /// <summary>
    /// Has <see cref="Dispose"/> been called?
    /// </summary>
    private bool isDisposed = false;

    /// <summary>
    /// Initialize a new <see cref="ChunkRenderBatch"/>.
    /// </summary>
    /// <param name="Triangle">Append buffer holding the generated <see cref="Triangle"/> data.</param>
    /// <param name="Args">Indirect arguments buffer (5 uints) produced after CopyCount.</param>
    /// <param name="keys">Chunk keys included in this batch (for bounds computation).</param>
    /// <param name="services">Layout/services used to convert chunk keys to world space.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="Args"/> is null.</exception>
    public ChunkRenderBatch(ComputeBuffer Triangle, ComputeBuffer Args, IReadOnlyList<ChunkKey> keys, IChunkServices services)
    {
        if (Args == null)
            throw new System.ArgumentNullException("args");

        this.Triangle = Triangle;
        this.Args = Args;
        this.Bounds = this.ComputeBounds(keys, services);
    }

    /// <summary>
    /// Release GPU buffers owned by this batch.
    /// Idempotent; safe to call during teardown. Do not dispose while a readback is in flight.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed) return;
        this.isDisposed = true;

        if (Args != null) Args.Dispose();
        if (Triangle != null) Triangle.Dispose();

        Args = null;
        Triangle = null;
    }

    /// <summary>
    /// Compute world-space bounds from the batch's keys.
    /// Padding should roughly match your chunk world size so triangles at edges aren't culled.
    /// </summary>
    private Bounds ComputeBounds(IReadOnlyList<ChunkKey> chunkContexts, IChunkServices services)
    {
        if (chunkContexts.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = services.Layout.ToWorld(chunkContexts[0]);
        Vector3 max = min;

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

    /// <summary>
    /// Asynchronously read all triangles from this batch's triangle buffer.
    /// Useful for LOD0 collider baking.
    /// </summary>
    /// <param name="set">Batch whose triangle buffer will be read.</param>
    /// <param name="onDone">Callback with the CPU-side triangle array (may be empty).</param>
    public static void ReadTrianglesAsync(ChunkRenderBatch set, System.Action<Triangle[]> onDone)
    {
        if (set.isDisposed)
        {
            throw new System.InvalidOperationException("Set has been disposed of.");
        }

        uint triCount = GetAppendCount(set.Triangle);
        if (triCount == 0) { onDone(System.Array.Empty<Triangle>()); return; }

        int stride = Marshal.SizeOf<Triangle>(); // e.g., 96 if using float4s
        int size = (int)(triCount * stride);

        AsyncGPUReadback.Request(set.Triangle, size, 0, req =>
        {
            if (req.hasError) { onDone(System.Array.Empty<Triangle>()); return; }
            onDone(req.GetData<Triangle>().ToArray());
        });
    }

    /// <summary>
    /// Return the number of appended elements in an AppendStructuredBuffer via CopyCount.
    /// </summary>
    private static uint GetAppendCount(ComputeBuffer append)
    {
        using var raw = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(append, raw, 0);
        var u = new uint[1];
        raw.GetData(u);
        return u[0];
    }
}
