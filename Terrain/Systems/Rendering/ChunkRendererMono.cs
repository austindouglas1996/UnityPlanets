namespace MarchingTerrain.Systems.Rendering
{
    using MarchingTerrain.Core;
    using MarchingTerrain.EditorSupport;
    using MarchingTerrain.Engine.Generation;
    using MarchingTerrain.Systems.Generation;
    using MarchingTerrain.Systems.Rendering.Unity;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.Rendering.Universal;

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
        [Tooltip("A URP Renderer that has the ChunkRenderFeature. Terrain renders on ANY renderer " +
                 "that includes the feature, so also add the feature to the renderer your camera " +
                 "actually uses (e.g. a third-party renderer you keep as Default).")]
        [SerializeField] private UniversalRendererData rendererData;

        [Header("Debug")]
        [HideInInspector] private bool isInitialized = false;

        [Header("Generation")]
        [Tooltip("Initial range for LOD4 chunk loading.")]
        [SerializeField]
        private ChunkRenderDistance RootRange = new ChunkRenderDistance();

        private ChunkRenderFeature renderFeature;
        private IChunkServices chunkServices;
        private ChunkGenerationProcessor processor;
        private ChunkLodOctree lodTree;

        public ChunkLodOctree LODTree => lodTree;

        int op = 1;

        /// <summary>
        /// Update the chunk layout and render any available chunks.
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;
            lodTree.Update();

            /*
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var keys = chunkServices.EditStore.Add(Follower.transform.position, op);

                foreach (var key in keys)
                    lodTree.EnsureNodeForEdit(key);
            }

            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
                op = op == 1 ? 0 : 1;
            */
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
            renderFeature = rendererData.rendererFeatures.OfType<ChunkRenderFeature>().FirstOrDefault();

            if (renderFeature == null)
            {
                Debug.LogError($"The renderer '{rendererData.name}' does not contain a {nameof(ChunkRenderFeature)}.", this);
                return;
            }

            this.chunkServices = services;
            this.processor = new ChunkGenerationProcessor(this.chunkServices, this.renderFeature);

            isInitialized = true;

            this.InitializeRootChunks();
        }

        public void Refresh()
        {
            this.processor.Refresh();
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