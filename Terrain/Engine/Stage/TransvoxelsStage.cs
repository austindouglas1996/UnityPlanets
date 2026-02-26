namespace GingerVoxelSystem.Engine.Stage
{
    using GingerVoxelSystem.Engine.Helpers;
    using GingerVoxelSystem.Engine.Options;
    using GingerVoxelSystem.Systems.Rendering;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Handles both marching-cubes passes:
    /// 1) PrePass: counts triangles per chunk
    /// 2) Main pass: emits actual triangle data
    /// </summary>
    public class TransVoxelsStage : IMarchingShader
    {
        private readonly int countTrianglesKernel;
        private readonly int marchKernel;

        private readonly ComputeShader transvoxelShader;

        /// <summary>
        /// Creates a new <see cref="MarchingCubesStage"/> and wires up the kernels,
        /// constant buffers, and static lookup tables used by the marching-cubes passes.
        /// </summary>
        /// <param name="transvoxelShader">Compute shader that contains the marching-cubes kernels.</param>
        /// <param name="buffers">Shared GPU buffers used across the terrain pipeline.</param>
        public TransVoxelsStage(ComputeShader transvoxelShader, ChunkBuffers buffers)
        {
            if (transvoxelShader == null)
            {
                throw new System.ArgumentNullException("MarchingCubes shader is null.");
            }

            this.transvoxelShader = transvoxelShader;

            countTrianglesKernel = transvoxelShader.FindKernel("RunMarchingCubesPrePass");
            marchKernel = transvoxelShader.FindKernel("RunMarchingCubes");

            // Shared constant buffers for both kernels
            transvoxelShader.SetConstantBuffer("TerrainDensityOptions", buffers.DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());
            transvoxelShader.SetConstantBuffer("PlanetDensityOptions", buffers.PlanetOptionsBuffer, 0, Marshal.SizeOf<PlanetDensityOptions>());

            // Static bindings: chunk metadata shared across both passes
            transvoxelShader.SetBuffer(countTrianglesKernel, "ChunkEdits", buffers.GenerateChunkEditBuffer); // Used for editing of chunks.
            transvoxelShader.SetBuffer(countTrianglesKernel, "ChunkInputs", buffers.GenerateChunkInputBuffer);
            transvoxelShader.SetBuffer(marchKernel, "ChunkInputs", buffers.GenerateChunkInputBuffer);
            transvoxelShader.SetBuffer(marchKernel, "ChunkEdits", buffers.GenerateChunkEditBuffer); // Used for editing of chunks.

            TransvoxelGpuTables.SetBuffer(transvoxelShader, countTrianglesKernel);
            TransvoxelGpuTables.SetBuffer(transvoxelShader, marchKernel);
        }

        /// <summary>
        /// Prepass that counts triangles for each cube in the chunk.
        /// Used to allocate correct output ranges during repack.
        /// </summary>
        public void DispatchTriangleCount(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset)
        {
            transvoxelShader.SetInt("Offset", offset);

            transvoxelShader.SetBuffer(countTrianglesKernel, "DensityMap", batch.DensityMap);
            transvoxelShader.SetBuffer(countTrianglesKernel, "TriangleCount", batch.TriangleChunkCounts);

            transvoxelShader.Dispatch(countTrianglesKernel, groupsX, groupsY, groupsZ);
        }

        /// <summary>
        /// Main marching-cubes pass that emits the actual triangles for the batch.
        /// Writes raw triangle data plus assigns initial detail metadata.
        /// </summary>
        public void DispatchMarching(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset)
        {
            transvoxelShader.SetInt("Offset", offset);

            transvoxelShader.SetBuffer(marchKernel, "DensityMap", batch.DensityMap);
            transvoxelShader.SetBuffer(marchKernel, "InitialDetailBuffer", batch.Details);
            transvoxelShader.SetBuffer(marchKernel, "TriangleSourceBuffer", batch.RawTriangleBuffer);
            transvoxelShader.SetBuffer(marchKernel, "TriangleCursor", batch.TriangleWriteCursor);

            transvoxelShader.Dispatch(marchKernel, groupsX, groupsY, groupsZ);
        }

        public void Dispose()
        {
            TransvoxelGpuTables.Dispose();
        }
    }
}
