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

    public ComputeShader Shader;

    [Tooltip("Helps with configuration of each chunk on this planet.")]
    public PlanetChunkConfiguration ChunkConfiguration;

    /// <summary>
    /// Manages the chunks made throughout this planet.
    /// </summary>
    private ChunkManager chunkManager;

    private void OnValidate()
    {
        if (this.chunkManager != null)
        {
            //this.lastSurfaceGradient = ChunkConfiguration.MapOptions.SurfaceColorRange;
            this.chunkManager.UpdateChunkColors();
        }
    }

    private void Awake()
    {
        //this.Follower.gameObject.SetActive(false);
        this.Follower.position = new Vector3(0, PlanetRadius * 2, 0);

        this.chunkManager = this.GetComponent<ChunkManager>();
        chunkManager.Follower = Follower;
        chunkManager.Initialize(ChunkConfiguration, new PlanetChunkLayout(this, ChunkConfiguration), new PlanetChunkControllerFactory(this));
    }
}