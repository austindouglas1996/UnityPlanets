using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class LandMass : MonoBehaviour, IChunkServices
{
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    [Tooltip("Helps with configuration of each chunk on this planet.")]
    public LandMassChunkConfiguration ChunkConfiguration;

    private ChunkManager chunkManager;

    private LandMassChunkColorizer colorizer;
    private LandMassChunkGenerator generator;
    private LandMassChunkLayout layout;
    private LandMassChunkControllerFactory factory;

    private void Awake()
    {
        this.chunkManager = this.GetComponent<ChunkManager>();

        colorizer = new LandMassChunkColorizer(ChunkConfiguration);
        generator = new LandMassChunkGenerator(this);
        layout = new LandMassChunkLayout(ChunkConfiguration);
        factory = new LandMassChunkControllerFactory(200, this, this.chunkManager.transform);

        this.chunkManager.Initialize(this.Follower, this);
    }

    IChunkConfiguration IChunkServices.Configuration => this.ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => this.layout;
    IChunkGenerator IChunkServices.Generator => this.generator;
    IChunkControllerFactory IChunkServices.ControllerFactory => this.factory;
    IChunkColorizer IChunkServices.Colorizer => this.colorizer;
}