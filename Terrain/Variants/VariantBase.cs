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

    protected IChunkGenerator generator;
    protected IChunkLayout layout;

    protected virtual void Awake()
    {
        chunkManager = GetComponent<ChunkManager>();
        chunkRenderer = GetComponent<ChunkRenderer>();

        generator = CreateGenerator();
        layout = CreateLayout();

        chunkManager.Initialize(Follower, this);
        chunkRenderer.Initialize(chunkManager, this);
    }

    // Abstracts to be implemented by derived classes
    protected abstract IChunkGenerator CreateGenerator();
    protected abstract IChunkLayout CreateLayout();

    // IChunkServices implementation
    IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => layout;
    IChunkGenerator IChunkServices.Generator => generator;
    ChunkManager IChunkServices.ChunkManager => chunkManager;
}
