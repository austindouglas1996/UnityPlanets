namespace GingerVoxelSystem
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Systems.Generation;
    using GingerVoxelSystem.Systems.Rendering;
    using UnityEngine;

    /// <summary>
    /// Unity-facing entry point for terrain generation.
    /// Ties together chunk layout, rendering, and a chosen generator (default: marching cubes).
    /// </summary>
    [RequireComponent(typeof(IChunkGenerator))]
    [RequireComponent(typeof(ChunkRendererMono))]
    public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
    {
        [Tooltip("Configuration for terrain generation.")]
        public ChunkConfiguration ChunkConfiguration;

        protected ChunkRendererMono chunkRenderer;
        protected IChunkGenerator chunkGenerator;

        private ChunkMath cmath;
        private ChunkEditStore edits;

        /// <summary>
        /// Set up layout and renderer, and pick the generator backend.
        /// </summary>
        protected virtual void Awake()
        {
            chunkGenerator = GetComponent<IChunkGenerator>();
            chunkRenderer = GetComponent<ChunkRendererMono>();

            if (ChunkConfiguration == null)
                Debug.LogError("ChunkConfiguration is not assigned.");

            chunkRenderer.Initialize(this);
            cmath = new ChunkMath(this.ChunkConfiguration);
            edits = new ChunkEditStore(this);
        }

        /// <summary>
        /// Called when values change in the inspector.
        /// Pushes updated options into the generator and refreshes chunks.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying || chunkGenerator == null)
                return;

            chunkGenerator.UpdateOptions();
            chunkRenderer.RefreshChunks();
        }

        IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
        IChunkGenerator IChunkServices.Generator => chunkGenerator;
        ChunkLodOctree IChunkServices.Octree => chunkRenderer.LODTree;
        ChunkMath IChunkServices.CMath => cmath;
        ChunkEditStore IChunkServices.EditStore => edits;
    }
}