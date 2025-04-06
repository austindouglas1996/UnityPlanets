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

    private ChunkManager chunkManager;

    /// <summary>
    /// Manages the chunks made throughout this planet.
    /// </summary>
    private ChunkManager ChunkManager;

    private void Awake()
    {
        //this.Follower.gameObject.SetActive(false);
        this.Follower.position = new Vector3(0, PlanetRadius * 2, 0);

        string e = "";

        this.chunkManager = this.GetComponent<ChunkManager>();
        chunkManager.Follower = Follower;
        chunkManager.Initialize(ChunkConfiguration, new PlanetChunkLayout(this, ChunkConfiguration), new PlanetChunkControllerFactory(this));
    }
}