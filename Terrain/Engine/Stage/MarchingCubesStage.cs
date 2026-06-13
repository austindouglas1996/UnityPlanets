using GingerVoxelSystem.Engine.Helpers;
using GingerVoxelSystem.Engine.Options;
using GingerVoxelSystem.Systems.Rendering;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GingerVoxelSystem.Engine.Stage
{
    /// <summary>
    /// Handles both marching-cubes passes:
    /// 1) PrePass: counts triangles per chunk
    /// 2) Main pass: emits actual triangle data
    /// 
    /// NOTE: This was replaced with Transvoxels. This has been kept as a REFERENCE only
    /// THIS IS NOT MAINTAINED NOR UPDATED.
    /// </summary>
    public class MarchingCubesStage : IDisposable
    {
        private readonly int countTrianglesKernel;
        private readonly int marchKernel;

        private readonly ComputeShader marchShader;
        private readonly ChunkBuffers buffers;

        ComputeBuffer cornerOffsets = MarchingCubesTables.CornerOffsetsBuffer();
        ComputeBuffer edgeConnections = MarchingCubesTables.EdgeConnectionsBuffer();
        ComputeBuffer triangleTable = MarchingCubesTables.TriangleTableBuffer();

        /// <summary>
        /// Creates a new <see cref="MarchingCubesStage"/> and wires up the kernels,
        /// constant buffers, and static lookup tables used by the marching-cubes passes.
        /// </summary>
        /// <param name="marchShader">Compute shader that contains the marching-cubes kernels.</param>
        /// <param name="buffers">Shared GPU buffers used across the terrain pipeline.</param>
        public MarchingCubesStage(ComputeShader marchShader, ChunkBuffers buffers)
        {
            if (marchShader == null)
            {
                throw new System.ArgumentNullException("MarchingCubes shader is null.");
            }

            this.marchShader = marchShader;
            this.buffers = buffers;

            countTrianglesKernel = marchShader.FindKernel("RunMarchingCubesPrePass");
            marchKernel = marchShader.FindKernel("RunMarchingCubes");

            // Shared constant buffers for both kernels
            marchShader.SetConstantBuffer("TerrainDensityOptions", buffers.DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());

            // Static bindings: chunk metadata shared across both passes
            marchShader.SetBuffer(countTrianglesKernel, "ChunkInputs", buffers.GenerateChunkInputBuffer);
            marchShader.SetBuffer(marchKernel, "ChunkInputs", buffers.GenerateChunkInputBuffer);

            // Marching cubes lookup tables (never change)
            marchShader.SetBuffer(countTrianglesKernel, "CornerOffsetsBuffer", cornerOffsets);
            marchShader.SetBuffer(countTrianglesKernel, "EdgeConnectionsBuffer", edgeConnections);
            marchShader.SetBuffer(countTrianglesKernel, "TriangleTableBuffer", triangleTable);

            marchShader.SetBuffer(marchKernel, "CornerOffsetsBuffer", cornerOffsets);
            marchShader.SetBuffer(marchKernel, "EdgeConnectionsBuffer", edgeConnections);
            marchShader.SetBuffer(marchKernel, "TriangleTableBuffer", triangleTable);
        }

        /// <summary>
        /// Prepass that counts triangles for each cube in the chunk.
        /// Used to allocate correct output ranges during repack.
        /// </summary>
        public void DispatchTriangleCount(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset)
        {
            marchShader.SetInt("Offset", offset);

            marchShader.SetBuffer(countTrianglesKernel, "DensityMap", batch.DensityMap);
            marchShader.SetBuffer(countTrianglesKernel, "TriangleCount", batch.TriangleChunkCounts);

            marchShader.Dispatch(countTrianglesKernel, groupsX, groupsY, groupsZ);
        }

        /// <summary>
        /// Main marching-cubes pass that emits the actual triangles for the batch.
        /// Writes raw triangle data plus assigns initial detail metadata.
        /// </summary>
        public void DispatchMarching(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset)
        {
            marchShader.SetInt("Offset", offset);

            marchShader.SetBuffer(marchKernel, "DensityMap", batch.DensityMap);
            marchShader.SetBuffer(marchKernel, "InitialDetailBuffer", batch.Details);
            marchShader.SetBuffer(marchKernel, "TriangleSourceBuffer", batch.RawTriangleBuffer);
            marchShader.SetBuffer(marchKernel, "TriangleCursor", batch.TriangleWriteCursor);

            marchShader.Dispatch(marchKernel, groupsX, groupsY, groupsZ);
        }

        public void Dispose()
        {
            cornerOffsets?.Dispose();
            edgeConnections?.Dispose();
            triangleTable?.Dispose();
        }
    }
}