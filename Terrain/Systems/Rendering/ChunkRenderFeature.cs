namespace GingerVoxelSystem.Systems.Rendering
{
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// URP renderer feature that injects the chunk terrain render pass.
    /// Added directly to the active renderer asset in the project settings.
    /// Holds a reference to the active <see cref="ChunkRenderRouter"/> so
    /// terrain draw commands can be recorded each frame.
    /// </summary>
    public class ChunkRenderFeature : ScriptableRendererFeature
    {
        // Router is assigned at runtime by the terrain system.
        [System.NonSerialized] public ChunkRenderRouter Router;

        private ChunkRenderPass pass;

        /// <summary>
        /// Called when the renderer feature is created or rebuilt.
        /// Instantiates the custom pass using the assigned router.
        /// </summary>
        public override void Create()
        {
            pass = new ChunkRenderPass(Router);
        }

        /// <summary>
        /// Injects the custom render pass into URP’s frame sequence.
        /// Runs every frame once the renderer is ready.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (Router != null && pass != null)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass.Dispose();
            base.Dispose(disposing);
        }
    }

}