namespace GingerVoxelSystem.Systems.Rendering
{
    using UnityEngine;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.EditorSupport;
    using GingerVoxelSystem.Engine.Generation;
    using GingerVoxelSystem.Systems.Generation;

    /// <summary>
    /// Unity-facing host for chunk rendering:
    /// - Ticks generator + octrees (Update)
    /// - Applies finalized job results on the main thread via the processor (LateUpdate)
    /// - Triggers drawing (OnRenderObject)
    ///
    /// Architectural seam:
    ///   This component owns the processor. The processor is the only thing that mutates
    ///   the render router. We never call the router directly from here.
    /// </summary>
    public class ChunkRendererMono : MonoBehaviour
    {
        [Tooltip("The main character of the world. The object we should spawn chunks around.")]
        public Transform Follower;

        [Header("Rendering")]
        [Tooltip("Assign the ChunkRenderFeature from your URP Renderer asset.")]
        [SerializeField] private ChunkRenderFeature renderFeature;

        [Header("Debug")]
        [HideInInspector] private bool isInitialized = false;

        [Header("Generation")]
        [Tooltip("Initial range for LOD4 chunk loading.")]
        [SerializeField]
        private ChunkRenderDistance RootRange = new ChunkRenderDistance();

        private IChunkServices chunkServices;
        private ChunkGenerationProcessor processor;
        private ChunkLodOctree lodTree;

        public ChunkLodOctree LODTree => lodTree;

        /// <summary>
        /// Update the chunk layout and render any available chunks.
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;
            lodTree.Update();
        }

        /// <summary>
        /// Ran after the initial update function.
        /// </summary>
        private void LateUpdate()
        {
            if (!isInitialized) return;

            processor.Update();
        }

        /// <summary>
        /// Dispose of the generator resources.
        /// </summary>
        private void OnDestroy()
        {
            this.processor.Dispose();
            this.chunkServices.Generator.Dispose();
        }

        /// <summary>
        /// Initialize the <see cref="ChunkRendererMono"/> to create initial chunks and start rendering.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="services"></param>
        public void Initialize(IChunkServices services)
        {
            this.chunkServices = services;
            this.processor = new ChunkGenerationProcessor(this.chunkServices, this.renderFeature);

            isInitialized = true;

            this.InitializeRootChunks();
        }

        /// <summary>
        /// Refresh chunks now.
        /// </summary>
        public void RefreshChunks()
        {
            this.processor.RemoveAll();
            this.InitializeRootChunks();
        }

        /// <summary>
        /// Create the initial root chunks in the world.
        /// </summary>
        private void InitializeRootChunks()
        {
            if (!isInitialized)
                return;

            if (RootRange == null)
            {
                Debug.LogError("Initial Root Range is null.");
                throw new System.ArgumentException("Chunk initial loading range is not set.");
            }

            // Create manager.
            lodTree = new ChunkLodOctree(this.chunkServices.Configuration, this.Follower, this.processor);

            // Create root nodes.
            Vector3Int span = RootRange.Span;
            for (int dx = -RootRange.X; dx <= RootRange.X; dx++)
                for (int dy = -RootRange.Down; dy <= RootRange.Up; dy++)
                    for (int dz = -RootRange.Z; dz <= RootRange.Z; dz++)
                        lodTree.AddRoot(new Vector3Int(dx, dy, dz));
        }
    }
}