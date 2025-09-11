using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
[RequireComponent(typeof(ChunkLayoutMono))]
[RequireComponent(typeof(ChunkRendererMono))]
public abstract class VariantBaseMono<TConfig> : MonoBehaviour, IChunkServices where TConfig : IChunkConfiguration
{
    [Tooltip("Configuration for how chunks behave.")]
    public TConfig ChunkConfiguration;

    protected ChunkLayoutMono chunkLayout;
    protected ChunkRendererMono chunkRenderer;

    protected IChunkGenerator generator;
    protected IChunkLayout layout;

    protected virtual void Awake()
    {
        //Application.targetFrameRate = 244;

        chunkLayout = GetComponent<ChunkLayoutMono>();
        chunkRenderer = GetComponent<ChunkRendererMono>();

        generator = CreateGenerator();
        layout = CreateLayout();

        chunkLayout.Initialize(layout);
        chunkRenderer.Initialize(this);
    }

    protected virtual void OnValidate()
    {
        if (!Application.isPlaying || generator == null)
            return;

        generator.UpdateOptions();
        chunkRenderer.RefreshChunks();
    }

    protected virtual void Update()
    {
        ConsoleTimer.WriteToConsole();
    }

    [RuntimeInitializeOnLoadMethod]
    static void InitLogging()
    {
        Application.logMessageReceived += (condition, stackTrace, type) =>
        {
            System.Console.WriteLine(condition);
        };
    }

    // Abstracts to be implemented by derived classes
    protected abstract IChunkGenerator CreateGenerator();
    protected abstract IChunkLayout CreateLayout();

    // IChunkServices implementation
    IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => layout;
    IChunkGenerator IChunkServices.Generator => generator;
}
