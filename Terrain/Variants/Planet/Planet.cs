using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class Planet : MonoBehaviour, IChunkServices
{
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    [Tooltip("Controls the size of the planet radius")]
    public int PlanetRadius = 32;

    [Tooltip("You better have some very good reasons for modifying this.")]
    public Vector3 Center { get; private set; } = Vector3.zero;

    [Tooltip("Helps with configuration of each chunk on this planet.")]
    public PlanetChunkConfiguration ChunkConfiguration;

    /// <summary>
    /// Manages the chunks made throughout this planet.
    /// </summary>
    private ChunkManager chunkManager;

    private PlanetChunkColorizer colorizer;
    private PlanetChunkGenerator generator;
    private PlanetChunkLayout layout;
    private PlanetChunkControllerFactory factory;

    private void Awake()
    {
        this.chunkManager = this.GetComponent<ChunkManager>();

        colorizer = new PlanetChunkColorizer(ChunkConfiguration);
        generator = new PlanetChunkGenerator(this, colorizer);
        layout = new PlanetChunkLayout(this, generator, ChunkConfiguration);
        factory = new PlanetChunkControllerFactory(this, 200, this, this.chunkManager.transform);

        this.chunkManager.Initialize(this.Follower, this);
    }

    IChunkConfiguration IChunkServices.Configuration => this.ChunkConfiguration;
    IChunkLayout IChunkServices.Layout => this.layout;
    IChunkGenerator IChunkServices.Generator => this.generator;
    IChunkControllerFactory IChunkServices.ControllerFactory => this.factory;
    IChunkColorizer IChunkServices.Colorizer => this.colorizer;
}