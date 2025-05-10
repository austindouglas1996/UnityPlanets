using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class Planet : MonoBehaviour
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

    private void Awake()
    {
        this.chunkManager = this.GetComponent<ChunkManager>();

        var generator = new PlanetChunkGenerator(this);
        var layout = new PlanetChunkLayout(this, generator, ChunkConfiguration);
        var factory = new PlanetChunkControllerFactory(this, 200, this.chunkManager);
        var colorizer = new PlanetChunkColorizer();

        this.chunkManager.Initialize(this.Follower, ChunkConfiguration, layout, colorizer, factory, generator);
    }
}