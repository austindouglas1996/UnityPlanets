namespace MarchingTerrain.Systems.Rendering.Unity
{
    using System;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// Custom render pass for terrain chunks.
    /// Injected into URP after opaques so it composites with the active renderer.
    /// Works under both legacy and RenderGraph pipelines.
    /// </summary>
    public class ChunkRenderPass : ScriptableRenderPass, IDisposable
    {
        /// <summary>
        /// Live router, assigned by the feature each frame. Shared across every renderer
        /// so terrain draws regardless of which renderer is the active/default one.
        /// </summary>
        public ChunkRenderRouter Router;

        /// <summary>
        /// Configures when this pass runs. The router is supplied per-frame by the feature.
        /// </summary>
        public ChunkRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        /// <summary>
        /// The router is owned by the terrain system, not the pass, so there is nothing
        /// to release here.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Legacy URP execution path.
        /// Used when RenderGraph is disabled (Forward Renderer mode).
        /// </summary>
        /*
        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (Router == null)
                return;

            // Pull a pooled command buffer and fill it with draw calls from the router
            var cmd = CommandBufferPool.Get("ChunkTerrain");
            Router.FillCommandBuffer(cmd);

            // Submit to GPU as part of URP's render sequence
            context.ExecuteCommandBuffer(cmd);

            // Return buffer to pool
            CommandBufferPool.Release(cmd);
        }*/

        /// <summary>
        /// RenderGraph path.
        /// Called only if URP's RenderGraph mode is enabled.
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Router == null)
                return;

            var resources = frameData.Get<UniversalResourceData>();

            using var builder =
                renderGraph.AddRasterRenderPass<PassData>("ChunkTerrain", out var passData);

            passData.Router = Router;

            // Bind camera color + depth as actual render attachments
            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                data.Router.FillCommandBuffer(ctx.cmd);
            });
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
