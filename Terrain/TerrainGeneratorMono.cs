namespace GingerVoxelSystem
{
    using UnityEngine;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine;
    using GingerVoxelSystem.Systems.Rendering;

    /// <summary>
    /// Unity-facing entry point for terrain generation.
    /// Ties together chunk layout, rendering, and a chosen generator (default: marching cubes).
    /// </summary>
    [RequireComponent(typeof(ChunkRendererMono))]
    [RequireComponent(typeof(ChunkMaterialSettings))]
    public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
    {
        [Tooltip("Shader used for generation.")]
        [SerializeField] public ComputeShader MarchingCubes;

        [Tooltip("Configuration for terrain generation.")]
        public ChunkConfiguration ChunkConfiguration;

        protected ChunkRendererMono chunkRenderer;
        protected ChunkMaterialSettings materialManager;

        protected IChunkGenerator generator;

        /// <summary>
        /// Set up layout and renderer, and pick the generator backend.
        /// </summary>
        protected virtual void Awake()
        {
            chunkRenderer = GetComponent<ChunkRendererMono>();
            materialManager = GetComponent<ChunkMaterialSettings>();

            generator = new MarchingCubesChunkGenerator(this, MarchingCubes, materialManager.BaseMaterial);
   
            if (MarchingCubes == null)
                Debug.LogError("ComputeShader not assigned.");
            if (ChunkConfiguration == null)
                Debug.LogError("ChunkConfiguration not assigned.");

            chunkRenderer.Initialize(this);
        }

        /// <summary>
        /// Called when values change in the inspector.
        /// Pushes updated options into the generator and refreshes chunks.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying || generator == null)
                return;

            generator.UpdateOptions();
            chunkRenderer.RefreshChunks();
        }

        IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
        IChunkGenerator IChunkServices.Generator => generator;
    }
}