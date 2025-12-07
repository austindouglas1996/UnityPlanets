using GingerVoxelSystem.Systems.Rendering;
using UnityEngine;

namespace Assets.Scripts.Terrain.Engine.Stage
{
    /// <summary>
    /// Handles the repack phase of the pipeline. 
    /// The prepass computes packed triangle ranges and writes draw args.
    /// The main pass copies raw triangles into a tightly packed output buffer.
    /// </summary>
    public class RepackStage
    {
        private readonly int repackPreKernel;
        private readonly int repackKernel;

        private readonly ComputeShader repackShader;
        private readonly ChunkBuffers chunkBuffers;

        /// <summary>
        /// Creates a new <see cref="RepackStage"/> and wires up kernel IDs and static bindings.
        /// </summary>
        public RepackStage(ComputeShader repackShader, ChunkBuffers chunkBuffers)
        {
            this.repackShader = repackShader;
            this.chunkBuffers = chunkBuffers;

            // Correct kernel names based on your compute shader
            repackPreKernel = repackShader.FindKernel("RunRepackPrePass");
            repackKernel = repackShader.FindKernel("RunRepack");

            // Static bindings shared between both kernels
            repackShader.SetBuffer(repackPreKernel, "ChunkInputs", chunkBuffers.GenerateChunkInputBuffer);
            repackShader.SetBuffer(repackKernel, "ChunkInputs", chunkBuffers.GenerateChunkInputBuffer);
        }

        /// <summary>
        /// Prepass that computes per-chunk packed output ranges (DestStart)
        /// and writes the draw args + dispatch args.
        /// </summary>
        public void DispatchPrePass(ChunkRenderBatch renderBatch, int batchSize)
        {
            repackShader.SetInt("BatchSize", batchSize);

            repackShader.SetBuffer(repackPreKernel, "TriangleCount", renderBatch.TriangleChunkCounts);
            repackShader.SetBuffer(repackPreKernel, "ArgsBuffer", renderBatch.Args);
            repackShader.SetBuffer(repackPreKernel, "DispatchArgsBuffer", renderBatch.DispatchArgs);

            repackShader.Dispatch(repackPreKernel, 1, 1, 1);
        }

        /// <summary>
        /// Main repack pass. Copies triangles from the raw buffer into a 
        /// contiguous packed buffer used for rendering and the detail stage.
        /// </summary>
        public void DispatchRepack(ChunkRenderBatch renderBatch, int batchSize)
        {
            repackShader.SetBuffer(repackKernel, "TriangleSourceBuffer", renderBatch.RawTriangleBuffer);
            repackShader.SetBuffer(repackKernel, "TriangleDestBuffer", renderBatch.FlatTriangleBuffer);

            repackShader.Dispatch(repackKernel, batchSize, 1, 1);
        }
    }
}
