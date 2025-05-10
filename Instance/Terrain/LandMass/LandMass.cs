using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class LandMass : MonoBehaviour
{
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    [Tooltip("Helps with configuration of each chunk on this planet.")]
    public LandMassChunkConfiguration ChunkConfiguration;

    private ChunkManager chunkManager;

    private void Awake()
    {
        this.chunkManager = this.GetComponent<ChunkManager>();

        var generator = new LandMassChunkGenerator();
        var layout = new LandMassChunkLayout(generator, ChunkConfiguration);
        var factory = new LandMassChunkControllerFactory(200, this.chunkManager);
        var colorizer = new LandMassChunkColorizer();

        this.chunkManager.Initialize(this.Follower, ChunkConfiguration, layout, colorizer, factory, generator);
    }
}