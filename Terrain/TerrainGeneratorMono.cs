using System;
using UnityEngine;
using UnityEngine.Scripting;

[Serializable]
public class TerrainBaseConfiguration : BaseChunkConfiguration
{
}

public class TerrainBaseGenerator : BaseChunkGenerator
{
    public TerrainBaseGenerator(ChunkRendererMono renderer, IChunkServices services, Material chunkMat)
        : base(services.Configuration)
    {
        this.generator = new MarchingCubesTerrainGenerator(services, renderer.MarchingCubes, chunkMat);
    }

    public override ITerrainGenerator Generator
    {
        get { return generator; }
    }
    private MarchingCubesTerrainGenerator generator;
}

public class TerrainBaseLayout : BaseChunkLayout
{
    public TerrainBaseLayout(TerrainBaseConfiguration configuration)
        : base(configuration)
    {
    }
}

[RequireComponent(typeof(ChunkLayoutMono))]
[RequireComponent(typeof(ChunkRendererMono))]
public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
{
    [Tooltip("Configuration for how chunks behave.")]
    public TerrainBaseConfiguration ChunkConfiguration;

    [SerializeField]
    public Material ShaderA;

    protected ChunkLayoutMono chunkLayout;
    protected ChunkRendererMono chunkRenderer;

    protected IChunkGenerator generator;
    protected IChunkLayout layout;

    protected virtual void Awake()
    {
        //Application.targetFrameRate = 244;

        chunkLayout = GetComponent<ChunkLayoutMono>();
        chunkRenderer = GetComponent<ChunkRendererMono>();

        generator = new TerrainBaseGenerator(chunkRenderer, this, ShaderA);
        layout = new TerrainBaseLayout(ChunkConfiguration);

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

    IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => layout;
    IChunkGenerator IChunkServices.Generator => generator;
}
