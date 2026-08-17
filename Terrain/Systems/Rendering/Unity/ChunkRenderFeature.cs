namespace MarchingTerrain.Systems.Rendering.Unity
{
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// URP renderer feature that injects the chunk terrain render pass.
    ///
    /// Add this feature to EVERY URP renderer your cameras might render through —
    /// including a third-party renderer you keep as Default. The router is shared
    /// statically (see <see cref="Router"/>), so terrain draws on whichever renderer
    /// is active; it is no longer tied to one specific renderer being the default.
    /// </summary>
    public class ChunkRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Shared across every <see cref="ChunkRenderFeature"/> instance on every renderer,
        /// so terrain draws regardless of which renderer is currently the default/active one.
        /// Assigned at runtime by the terrain system (the generation processor).
        /// </summary>
        public static ChunkRenderRouter Router;

        private ChunkRenderPass pass;

        /// <summary>
        /// Called when the renderer feature is created or rebuilt.
        /// </summary>
        public override void Create()
        {
            pass = new ChunkRenderPass();
        }

        /// <summary>
        /// Injects the custom render pass into URP's frame sequence for the renderer
        /// currently rendering. The live shared router is handed to the pass each frame.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (Router == null || pass == null)
                return;

            pass.Router = Router;
            renderer.EnqueuePass(pass);
        }

        /// <summary>
        /// Dispose of the element.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            base.Dispose(disposing);
        }
    }
}
