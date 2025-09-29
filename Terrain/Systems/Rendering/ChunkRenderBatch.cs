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
    /// Append buffer containing generated detail data for triangles.
    /// </summary>
    public ComputeBuffer Details;

    /// <summary>
    /// Indirect draw arguments buffer (ComputeBufferType.IndirectArguments).
    /// </summary>
    public ComputeBuffer Args;

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
    public ChunkRenderBatch(ComputeBuffer Triangle, ComputeBuffer Details, ComputeBuffer Args, IReadOnlyList<ChunkKey> keys, IChunkServices services)
    {
        if (Args == null)
            throw new System.ArgumentNullException("args");

        this.Triangle = Triangle;
        this.Details = Details;
        this.Args = Args;
    }

    /// <summary>
    /// Returns whether this batch has been destroyed.
    /// </summary>
    public bool IsDisposed => isDisposed;

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
        if (Details != null) Details.Dispose();

        Args = null;
        Triangle = null;
        Details = null;
    }

    /// <summary>
    /// Asynchronously read all triangles from this batch's triangle buffer.
    /// Useful for LOD0 collider baking.
    /// </summary>
    /// <param name="set">Batch whose triangle buffer will be read.</param>
    /// <param name="onDone">Callback with the CPU-side triangle array (may be empty).</param>
    public static void ReadTrianglesAsync(ChunkRenderBatch set, System.Action<TriangleDataGPU[]> onDone)
    {
        if (set.isDisposed)
        {
            throw new System.InvalidOperationException("Set has been disposed of.");
        }

        uint triCount = GetAppendCount(set.Triangle);
        if (triCount == 0) { onDone(System.Array.Empty<TriangleDataGPU>()); return; }

        int stride = Marshal.SizeOf<TriangleDataGPU>();
        int size = (int)(triCount * stride);

        AsyncGPUReadback.Request(set.Triangle, size, 0, req =>
        {
            if (req.hasError) { onDone(System.Array.Empty<TriangleDataGPU>()); return; }
            onDone(req.GetData<TriangleDataGPU>().ToArray());
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
