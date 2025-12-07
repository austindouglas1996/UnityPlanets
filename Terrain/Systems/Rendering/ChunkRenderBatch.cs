namespace GingerVoxelSystem.Systems.Rendering
{
    using System;
    using UnityEngine;
    using UnityEngine.Rendering;
    using GingerVoxelSystem.Core;

    /// <summary>
    /// Draw-ready container for a chunk group: triangle append buffer + indirect args + culling bounds.
    /// Created by generation, consumed by rendering and (optionally) collider baking.
    /// </summary>
    public class ChunkRenderBatch : IDisposable
    {
        /// <summary>
        /// Append buffer containing generated triangles (ComputeBufferType.Append).
        /// </summary>
        public ComputeBuffer RawTriangleBuffer;

        /// <summary>
        /// Append buffer containing generated triangles in a flat list.
        /// </summary>
        public ComputeBuffer FlatTriangleBuffer;

        /// <summary>
        /// Small buffer for controlling the counts per chunk.
        /// </summary>
        public ComputeBuffer TriangleChunkCounts;

        /// <summary>
        /// Small buffer for the cursor.
        /// </summary>
        public ComputeBuffer TriangleWriteCursor;

        /// <summary>
        /// The density map used for the render batch.
        /// </summary>
        public ComputeBuffer DensityMap;

        /// <summary>
        /// Append buffer containing generated detail data for triangles.
        /// </summary>
        public ComputeBuffer Details;

        /// <summary>
        /// One of the buffers is unique.
        /// </summary>
        public ComputeBuffer DispatchArgs;

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
        /// <param name="Triangle">Append buffer holding the generated <see cref="RawTriangleBuffer"/> data.</param>
        /// <param name="Args">Indirect arguments buffer (5 uints) produced after CopyCount.</param>
        /// <param name="keys">Chunk keys included in this batch (for bounds computation).</param>
        /// <param name="services">Layout/services used to convert chunk keys to world space.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="Args"/> is null.</exception>
        public ChunkRenderBatch(
            ComputeBuffer TriangleSource, 
            ComputeBuffer TriangleDest, 
            ComputeBuffer triangleCounts, 
            ComputeBuffer triangleCursor, 
            ComputeBuffer Details, 
            ComputeBuffer densityMap)
        {
            this.RawTriangleBuffer = TriangleSource;
            this.FlatTriangleBuffer = TriangleDest;
            this.TriangleWriteCursor = triangleCursor;
            this.TriangleChunkCounts = triangleCounts;
            this.Details = Details;
            this.DensityMap = densityMap;
            this.Args = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            this.DispatchArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
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
            if (RawTriangleBuffer != null) RawTriangleBuffer.Dispose();
            if (FlatTriangleBuffer != null) FlatTriangleBuffer.Dispose();
            if (TriangleChunkCounts != null) TriangleChunkCounts.Dispose();
            if (TriangleWriteCursor != null) TriangleWriteCursor.Dispose();
            if (Details != null) Details.Dispose();
            if (DensityMap != null) DensityMap.Dispose();

            Args = null;
            RawTriangleBuffer = null;
            FlatTriangleBuffer = null;
            TriangleChunkCounts = null;
            TriangleWriteCursor = null;
            Details = null;
            DensityMap = null;
        }

        /// <summary>
        /// Asynchronously read all triangles from this batch's triangle buffer.
        /// Useful for LOD0 collider baking.
        /// </summary>
        /// <param name="set">Batch whose triangle buffer will be read.</param>
        /// <param name="onDone">Callback with the CPU-side triangle array (may be empty).</param>
        public static void ReadTrianglesAsync(ChunkRenderBatch set, Action<TriangleDataGPU[]> onDone)
        {
            if (set.isDisposed)
            {
                throw new System.InvalidOperationException("Set has been disposed of.");
            }

            AsyncGPUReadback.Request(set.FlatTriangleBuffer, rTris =>
            {
                if (rTris.hasError) { onDone(Array.Empty<TriangleDataGPU>()); return; }

                var output = rTris.GetData<TriangleDataGPU>().ToArray();
                onDone(output);
            });
        }
    }
}