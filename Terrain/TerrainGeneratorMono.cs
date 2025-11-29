namespace GingerVoxelSystem
{
    using UnityEngine;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine;
    using GingerVoxelSystem.Helpers;
    using GingerVoxelSystem.Systems.Chunks;
    using GingerVoxelSystem.Systems.Rendering;

    /// <summary>
    /// Unity-facing entry point for terrain generation.
    /// Ties together chunk layout, rendering, and a chosen generator (default: marching cubes).
    /// </summary>
    [RequireComponent(typeof(ChunkLayoutMono))]
    [RequireComponent(typeof(ChunkRendererMono))]
    [RequireComponent(typeof(ChunkMaterialSettings))]
    public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
    {
        [Tooltip("Shader used for generation.")]
        [SerializeField] public ComputeShader MarchingCubes;

        [Tooltip("Configuration for terrain generation.")]
        public BaseChunkConfiguration ChunkConfiguration;

        [SerializeField] protected ChunkLayoutMono chunkLayout;
        [SerializeField] protected ChunkRendererMono chunkRenderer;
        [SerializeField] protected ChunkMaterialSettings materialManager;

        protected IChunkGenerator generator;
        protected IChunkLayout layout;

        /// <summary>
        /// Set up layout and renderer, and pick the generator backend.
        /// </summary>
        protected virtual void Awake()
        {
            chunkLayout = GetComponent<ChunkLayoutMono>();
            chunkRenderer = GetComponent<ChunkRendererMono>();
            materialManager = GetComponent<ChunkMaterialSettings>();

            generator = new MarchingCubesChunkGenerator(this, MarchingCubes, materialManager.BaseMaterial);
            layout = new BaseChunkLayout(ChunkConfiguration);

            if (MarchingCubes == null)
                Debug.LogError("ComputeShader not assigned.");
            if (ChunkConfiguration == null)
                Debug.LogError("ChunkConfiguration not assigned.");

            chunkLayout.Initialize(layout);
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

        /// <summary>
        /// Updated used for updating generator content.
        /// </summary>
        protected virtual void Update()
        {
            ConsoleTimer.WriteToConsole();
        }

        IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
        IChunkLayout IChunkServices.Layout => layout;
        IChunkGenerator IChunkServices.Generator => generator;
    }
}