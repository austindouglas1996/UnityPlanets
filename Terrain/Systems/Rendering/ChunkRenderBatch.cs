namespace MarchingTerrain.Systems.Rendering
{
    using System;
    using UnityEngine;

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
        /// The density map for the columns for the render batch.
        /// </summary>
        public ComputeBuffer DensitySurfaceMap;

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
        /// The material block used for generation.
        /// </summary>
        public MaterialPropertyBlock MPB { get; private set; }

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
            ComputeBuffer densityMap,
            ComputeBuffer densityColumnMap)
        {
            this.RawTriangleBuffer = TriangleSource;
            this.FlatTriangleBuffer = TriangleDest;
            this.TriangleWriteCursor = triangleCursor;
            this.TriangleChunkCounts = triangleCounts;
            this.Details = Details;
            this.DensityMap = densityMap;
            this.DensitySurfaceMap = densityColumnMap;
            this.Args = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            this.DispatchArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);

            this.MPB = new MaterialPropertyBlock();
            MPB.SetBuffer("_TriangleBuffer", this.FlatTriangleBuffer);
            MPB.SetBuffer("_TriangleDetailsBuffer", this.Details);
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
            if (DensitySurfaceMap != null) DensitySurfaceMap.Dispose();

            Args = null;
            RawTriangleBuffer = null;
            FlatTriangleBuffer = null;
            TriangleChunkCounts = null;
            TriangleWriteCursor = null;
            Details = null;
            DensityMap = null;
            DensitySurfaceMap = null;
            MPB = null;
        }
    }
}