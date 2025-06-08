using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public abstract class VariantBase<TConfig> : MonoBehaviour, IChunkServices where TConfig : IChunkConfiguration
{
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    [Tooltip("Configuration for how chunks behave.")]
    public TConfig ChunkConfiguration;

    protected ChunkManager chunkManager;
    protected ChunkRenderer chunkRenderer;

    protected IChunkColorizer colorizer;
    protected IChunkGenerator generator;
    protected IChunkLayout layout;
    protected IChunkControllerFactory factory;

    protected virtual void Awake()
    {
        chunkManager = GetComponent<ChunkManager>();
        chunkRenderer = GetComponent<ChunkRenderer>();

        colorizer = CreateColorizer();
        generator = CreateGenerator();
        layout = CreateLayout();
        factory = CreateFactory();

        chunkManager.Initialize(Follower, this);
        chunkRenderer.Initialize(chunkManager, this);
    }

    // Abstracts to be implemented by derived classes
    protected abstract IChunkColorizer CreateColorizer();
    protected abstract IChunkGenerator CreateGenerator();
    protected abstract IChunkLayout CreateLayout();
    protected abstract IChunkControllerFactory CreateFactory();

    // IChunkServices implementation
    IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => layout;
    IChunkGenerator IChunkServices.Generator => generator;
    IChunkControllerFactory IChunkServices.ControllerFactory => factory;
    IChunkColorizer IChunkServices.Colorizer => colorizer;
    ChunkManager IChunkServices.ChunkManager => chunkManager;
}
