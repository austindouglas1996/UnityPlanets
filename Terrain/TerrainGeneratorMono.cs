using UnityEngine;

/// <summary>
/// Unity-facing entry point for terrain generation.
/// Ties together chunk layout, rendering, and a chosen generator (default: marching cubes).
/// Attach this to a GameObject with <see cref="ChunkLayoutMono"/> and <see cref="ChunkRendererMono"/>.
/// </summary>
[RequireComponent(typeof(ChunkLayoutMono))]
[RequireComponent(typeof(ChunkRendererMono))]
public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
{
    [Tooltip("Shader used for generation.")]
    [SerializeField] public ComputeShader MarchingCubes;

    [Tooltip("Material used.")]
    [SerializeField] public Material ChunkMaterial;

    [Tooltip("Configuration for how chunks behave.")]
    public BaseChunkConfiguration ChunkConfiguration;

    protected ChunkLayoutMono chunkLayout;
    protected ChunkRendererMono chunkRenderer;

    protected IChunkGenerator generator;
    protected IChunkLayout layout;

    /// <summary>
    /// Set up layout and renderer, and pick the generator backend.
    /// </summary>
    protected virtual void Awake()
    {
        chunkLayout = GetComponent<ChunkLayoutMono>();
        chunkRenderer = GetComponent<ChunkRendererMono>();

        generator = new MarchingCubesChunkGenerator(this, MarchingCubes, ChunkMaterial);
        layout = new BaseChunkLayout(ChunkConfiguration);

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

    IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => layout;
    IChunkGenerator IChunkServices.Generator => generator;
}
