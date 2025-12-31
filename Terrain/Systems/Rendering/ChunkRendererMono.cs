namespace GingerVoxelSystem.Systems.Rendering
{
    using UnityEngine;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.EditorSupport;
    using GingerVoxelSystem.Engine.Generation;
    using GingerVoxelSystem.Systems.Generation;
    using System.Collections.Generic;

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
            //lodTree.Update();
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
            this.DebugChunks();
            //this.DebugChunks1();
            //this.DebugChunksA();
        }

        private void DebugChunks1()
        {
            // Create chunks
            ChunkKey k1 = new ChunkKey(new Vector3Int(0, 0, 0), 2);
            ChunkKey k2 = new ChunkKey(new Vector3Int(0, 0, -2), 1);
            ChunkKey k3 = new ChunkKey(new Vector3Int(4, 0, 0), 1);

            uint mask1 = 0; // k1 is coarser → NO transitions

            uint mask2 = 0;
            mask2 |= 1u << 5; // +Z (toward k1)

            uint mask3 = 0;
            mask3 |= 1u << 0; // +Z (toward k1)

            k1.Mask = mask1;
            k2.Mask = mask2;
            k3.Mask = mask3;


            // Inject into chunk system (however you store masks)
            this.processor.RequestChunkGeneration(k1, null);
            this.processor.RequestChunkGeneration(k2, null);
            this.processor.RequestChunkGeneration(k3, null);
        }


        private void DebugChunks()
        {
            ChunkKey k1 = new ChunkKey(new Vector3Int(0, 0, 0), 4);
            ChunkKey k2 = new ChunkKey(new Vector3Int(0, 0, -1), 3);
            ChunkKey k7 = new ChunkKey(new Vector3Int(0, 0, 2), 3);
            ChunkKey k3 = new ChunkKey(new Vector3Int(-1, 0, 1), 3);
            ChunkKey k4 = new ChunkKey(new Vector3Int(-1, 0, 0), 3);
            ChunkKey k5 = new ChunkKey(new Vector3Int(2, 0, 1), 3);
            ChunkKey k6 = new ChunkKey(new Vector3Int(2, 0, 0), 3);
            ChunkKey k8 = new ChunkKey(new Vector3Int(-3, 0, 0), 2);
            ChunkKey k9 = new ChunkKey(new Vector3Int(-7, 0, 0), 1);

            uint tMask1A = 0;
            tMask1A |= 1u << 0;
            tMask1A |= 1u << 1;
            tMask1A |= 1u << 2;
            tMask1A |= 1u << 3;
            tMask1A |= 1u << 4;
            tMask1A |= 1u << 5;

            uint tMask2A = 0;
            uint tMask3A = 0;
            tMask3A |= 1u << 0;
            uint tMask4A = 0;
            tMask4A |= 1u << 0;
            uint tMask5A = 0; 
            uint tMask6A = 0;
            tMask6A |= 1u << 1;
            uint tMask7A = 0;
            uint tMask8A = 0;
            tMask8A |= 1u << 0;
            uint tMask9A = 0;

            k1.Mask = tMask1A;
            k2.Mask = tMask2A;
            k3.Mask = tMask3A;
            k4.Mask = tMask4A;
            k5.Mask = tMask5A;
            k6.Mask = tMask6A;
            k7.Mask = tMask7A;
            k8.Mask = tMask8A;
            k9.Mask = tMask9A;


            // Inject into chunk system (however you store masks)
            this.processor.RequestChunkGeneration(k1, null);
            this.processor.RequestChunkGeneration(k2, null);
            //this.processor.RequestChunkGeneration(k3, null);
            this.processor.RequestChunkGeneration(k4, null);
            this.processor.RequestChunkGeneration(k5, null);
            this.processor.RequestChunkGeneration(k6, null);
            this.processor.RequestChunkGeneration(k7, null);
            this.processor.RequestChunkGeneration(k8, null);
            this.processor.RequestChunkGeneration(k9, null);
        }

        private void DebugChunksA()
        {
            // Create chunks
            ChunkKey k1 = new ChunkKey(new Vector3Int(0, 0, 0), 4);
            ChunkKey k2 = new ChunkKey(new Vector3Int(0, 0, -8), 3);
            ChunkKey k7 = new ChunkKey(new Vector3Int(0, 0, 16), 3);
            ChunkKey k3 = new ChunkKey(new Vector3Int(-8, 0, 8), 3);
            ChunkKey k4 = new ChunkKey(new Vector3Int(-8, 0, 0), 3);
            ChunkKey k5 = new ChunkKey(new Vector3Int(16, 0, 8), 3);
            ChunkKey k6 = new ChunkKey(new Vector3Int(16, 0, 0), 3);
            ChunkKey k8 = new ChunkKey(new Vector3Int(24, 0, 0), 2);

            uint mask1 = 0; // k1 is coarser → NO transitions

            uint mask2 = 0;
            mask2 |= 1u << 5; // +Z (toward k1)

            uint mask7 = 0;
            mask7 |= 1u << 4; // +Z (toward k1)

            uint mask3 = 0;
            mask3 |= 1u << 1; // -X (toward k1)

            uint mask4 = 0;
            mask4 |= 1u << 1; // -X (toward k1)

            uint mask5 = 0;
            mask5 |= 1u << 0; // +X (toward k1)

            uint mask6 = 0;
            mask6 |= 1u << 0; // +X (toward k1)

            uint mask8 = 0;
            mask8 |= 1u << 0; // +X (toward k1)

            k1.Mask = mask1;
            k2.Mask = mask2;
            k3.Mask = mask3;
            k4.Mask = mask4;
            k5.Mask = mask5;
            k6.Mask = mask6;
            k7.Mask = mask7;
            k8.Mask = mask8;


            // Inject into chunk system (however you store masks)
            this.processor.RequestChunkGeneration(k1, null);
            this.processor.RequestChunkGeneration(k2, null);
            this.processor.RequestChunkGeneration(k3, null);
            this.processor.RequestChunkGeneration(k4, null);
            this.processor.RequestChunkGeneration(k5, null);
            this.processor.RequestChunkGeneration(k6, null);
            this.processor.RequestChunkGeneration(k7, null);
            this.processor.RequestChunkGeneration(k8, null);
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