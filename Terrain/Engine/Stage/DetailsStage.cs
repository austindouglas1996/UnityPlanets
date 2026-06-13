namespace GingerVoxelSystem.Engine.Stage
{
    using GingerVoxelSystem.Engine;
    using GingerVoxelSystem.Engine.Options;
    using GingerVoxelSystem.Systems.Rendering;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Handles the detail pass for marching-cubes output. This stage computes per-triangle
    /// biome/lighting/color data after the triangles have been repacked.
    /// </summary>
    public class DetailsStage
    {
        private readonly int detailsPassKernel;

        private readonly ChunkBuffers buffers;
        private readonly ComputeShader detailsShader;

        /// <summary>
        /// Creates a new <see cref="DetailsStage"/> and binds the constant buffers shared across detail kernels.
        /// </summary>
        /// <param name="detailsShader">Compute shader containing the detail kernel.</param>
        /// <param name="chunkBuffer">Shared GPU buffers.</param>
        public DetailsStage(ComputeShader detailsShader, ChunkBuffers chunkBuffer)
        {
            if (detailsShader == null)
            {
                throw new System.ArgumentNullException("Details shader is null");
            }

            this.detailsShader = detailsShader;
            this.buffers = chunkBuffer;

            // Kernel lookup
            detailsPassKernel = detailsShader.FindKernel("RunDetailsPass");

            // Constant buffers (shared data for biome/color generation)
            this.detailsShader.SetConstantBuffer("TerrainDensityOptions", buffers.DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());
        }

        /// <summary>
        /// Dispatches the detail pass for a batch. This uses the repacked triangles and writes out
        /// per-triangle detail metadata (biome colors, blending, lod tagging, etc.).
        /// </summary>
        /// <param name="batch">The batch containing triangle buffers and indirect args.</param>
        public void DispatchDetailPass(ChunkRenderBatch batch)
        {
            // Bind dynamic buffers
            detailsShader.SetBuffer(detailsPassKernel, "DispatchArgsBuffer", batch.DispatchArgs);
            detailsShader.SetBuffer(detailsPassKernel, "DetailTriangles", batch.FlatTriangleBuffer);
            detailsShader.SetBuffer(detailsPassKernel, "DetailBuffer", batch.Details);

            // Dispatch using the triangle count computed earlier
            detailsShader.DispatchIndirect(detailsPassKernel, batch.DispatchArgs);
        }
    }
}
