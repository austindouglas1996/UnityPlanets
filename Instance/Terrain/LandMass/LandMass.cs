using UnityEngine;

[RequireComponent(typeof(ChunkManager))]
public class LandMass : MonoBehaviour
{
    [Tooltip("The main character of the world. The object we should spawn chunks around.")]
    public Transform Follower;

    [Tooltip("Helps with configuration of each chunk on this planet.")]
    public LandMassChunkConfiguration ChunkConfiguration;

    private ChunkManager chunkManager;

    private void OnValidate()
    {
        if (this.chunkManager != null)
        {
            this.chunkManager.UpdateChunkColors();
        }
    }

    private void Awake()
    {
        this.chunkManager = this.GetComponent<ChunkManager>();
        chunkManager.Follower = Follower;
        ChunkConfiguration.Setup();
        chunkManager.Initialize(ChunkConfiguration, new LandMassChunkLayout(new LandMassChunkGenerator(), ChunkConfiguration), new LandMassChunkControllerFactory());
    }
}