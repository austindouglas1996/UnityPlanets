namespace UnityTerrainGenerator.Systems.Rendering
{
    using System;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// Custom render pass for terrain chunks.
    /// Injected into URP after transparents so it lines up with shaders using queue ~3000.
    /// Works under both legacy and RenderGraph pipelines.
    /// </summary>
    public class ChunkRenderPass : ScriptableRenderPass, IDisposable
    {
        private ChunkRenderRouter router;

        /// <summary>
        /// Sets the router reference and when this pass should run.
        /// </summary>
        public ChunkRenderPass(ChunkRenderRouter router)
        {
            this.router = router;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public void Dispose()
        {
            if (router != null)
            {
                router = null;
            }
        }

        /// <summary>
        /// Legacy URP execution path.
        /// Used when RenderGraph is disabled (Forward Renderer mode).
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (router == null)
                return;

            // Pull a pooled command buffer and fill it with draw calls from the router
            var cmd = CommandBufferPool.Get("ChunkTerrain");
            router.FillCommandBuffer(cmd);

            // Submit to GPU as part of URP’s render sequence
            context.ExecuteCommandBuffer(cmd);

            // Return buffer to pool
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// RenderGraph path.
        /// Called only if URP's RenderGraph mode is enabled.
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (router == null)
                return;

            using (var builder = renderGraph.AddRenderPass<PassData>("ChunkTerrain", out var passData))
            {
                passData.Router = router;

                builder.SetRenderFunc((PassData data, RenderGraphContext ctx) =>
                {
                    // Same logic as legacy path but uses RenderGraph context command buffer
                    data.Router.FillCommandBuffer(ctx.cmd);
                });
            }
        }

        /// <summary>
        /// Simple container for data passed into the RenderGraph builder.
        /// </summary>
        private class PassData
        {
            public ChunkRenderRouter Router;
        }
    }

}