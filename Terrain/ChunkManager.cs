using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using VHierarchy.Libs;

/// <summary>
/// Manages all active chunks in the world. Handles loading, unloading, re-coloring,
/// and modifying terrain based on player movement and brush interactions.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    /// <summary>
    /// The transform that this chunk system follows, like the player.
    /// </summary>
    [HideInInspector] public Transform Follower;

    [Header("Rendering")]

    /// <summary>
    /// How far the follower has to move before we trigger an update of active chunks.
    /// </summary>
    [Tooltip("How far the follower needs to be travel before we update the active chunks.")]
    public float TravelDistanceToUpdateChunks = 10f;

    /// <summary>
    /// These components are used to help with the generation process.
    /// </summary>
    [SerializeField] private IChunkConfiguration Configuration;
    [SerializeField] private IChunkLayout Layout;
    [SerializeField] private IChunkControllerFactory Factory;

    /// <summary>
    /// Holds a collection of chunks we have seen along with active chunks that are currently active.
    /// </summary>
    private Dictionary<Vector3Int, ChunkController> ActiveChunks = new Dictionary<Vector3Int, ChunkController>();
    private Dictionary<Vector3Int, ChunkController> CacheChunks = new Dictionary<Vector3Int, ChunkController>();

    private Vector3 LastKnownFollowerPosition;

    private bool IsBusy = false;
    private bool IsInitialized = false;

    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private void Awake()
    {
        // Reset the last seen follower position so we update everything.
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);
    }

    private void Update()
    {
        if (IsFollowerOutsideOfRange())
        {
            UpdateChunks();
        }
    }

    void OnDisable()
    {
        cancellationToken.Cancel();
    }

    /// <summary>
    /// Sets up the chunk manager with the required configuration, layout, and factory.
    /// </summary>
    /// <param name="configuration">Settings for chunk size and behavior.</param>
    /// <param name="layout">Logic to determine visible chunk positions.</param>
    /// <param name="factory">Factory that builds new chunk controllers.</param>
    /// <exception cref="System.ArgumentNullException">If any required dependency is missing.</exception>
    public void Initialize(IChunkConfiguration configuration, IChunkLayout layout, IChunkControllerFactory factory)
    {
        if (configuration == null)
            throw new System.ArgumentNullException("Configuration is null.");
        if (layout == null)
            throw new System.ArgumentNullException("Layout is null.");
        if (factory == null)
            throw new System.ArgumentNullException("Factory is null.");

        this.Configuration = configuration;
        this.Layout = layout;
        this.Factory = factory;

        ChunkGenerationQueue.Instance.CancellationToken = cancellationToken.Token;

        this.IsInitialized = true;
    }

    /// <summary>
    /// Loops through all child chunks and reapplies their colors. Useful for debugging or updating style changes.
    /// </summary>
    public void UpdateChunkColors()
    {
        foreach (Transform child in this.transform)
        {
            ChunkController controller = child.GetComponent<ChunkController>();
            if (controller != null)
            {
                controller.ApplyChunkColors();
            }
        }
    }

    /// <summary>
    /// Destroys all current chunks and resets internal state. Useful for debugging.
    /// </summary>
    public void Restart()
    {
        this.ActiveChunks.Clear();
        this.CacheChunks.Clear();

        foreach (Transform child in this.transform)
        {
            Destroy(child.gameObject);
        }

        this.IsBusy = false;
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);
    }

    /// <summary>
    /// Modifies all chunks that intersect the brush area.
    /// Used when the player adds or removes terrain.
    /// </summary>
    /// <param name="brush">The terrain brush to apply.</param>
    /// <param name="isAdding">True to add terrain, false to remove.</param>
    /// <param name="bufferMultiplier">Optional chunk bounds buffer.</param>
    /// <param name="token">Optional cancellation token.</param>
    public async Task ModifyTerrain(TerrainBrush brush, bool isAdding, float bufferMultiplier = 0.5f, CancellationToken token = default)
    {
        Bounds brushBounds = brush.GetBrushBounds();
        Vector3 chunkSize = new Vector3(Configuration.ChunkSize, Configuration.ChunkSize, Configuration.ChunkSize);

        Vector3Int hitPosCoord = new Vector3Int(
            Mathf.FloorToInt(brush.WorldHitPoint.x / Configuration.ChunkSize),
            Mathf.FloorToInt(brush.WorldHitPoint.y / Configuration.ChunkSize),
            Mathf.FloorToInt(brush.WorldHitPoint.z / Configuration.ChunkSize)
        );

        // Check all neighbors in a 3x3x3 cube around the hit position
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    token.ThrowIfCancellationRequested();

                    Vector3Int neighborCoord = hitPosCoord + new Vector3Int(x, y, z);

                    if (ActiveChunks.TryGetValue(neighborCoord, out var chunk))
                    {
                        Bounds chunkBounds = new Bounds(chunk.transform.position + chunkSize * bufferMultiplier, chunkSize);

                        if (brushBounds.Intersects(chunkBounds))
                            await chunk.ModifyChunk(brush, isAdding, token);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rebuilds the list of active chunks based on the follower’s current position.
    /// Adds new chunks and removes out-of-range ones.
    /// </summary>
    private void UpdateChunks()
    {
        if (IsBusy || !IsInitialized)
            return;
        IsBusy = true;

        HashSet<Vector3Int> visibleChunksCoordinates = new HashSet<Vector3Int>(Layout.GetActiveChunkCoordinates(this.Follower.position));

        Vector3Int followerChunkPos = new Vector3Int(
            Mathf.FloorToInt(this.Follower.position.x / this.Configuration.ChunkSize),
            Mathf.FloorToInt(this.Follower.position.y / this.Configuration.ChunkSize),
            Mathf.FloorToInt(this.Follower.position.z / this.Configuration.ChunkSize));

        // Retrieve invalid chunks.
        List<Vector3Int> invalidChunks = new List<Vector3Int>();
        foreach (var key in this.ActiveChunks.Keys)
        {
            if (!visibleChunksCoordinates.Contains(key))
            {
                invalidChunks.Add(key);
            }
        }

        // Remove invalid chunks.
        foreach (var invalidKey in invalidChunks)
        {
            ActiveChunks.Remove(invalidKey);
        }

        // Add new chunks.
        foreach (var chunk in visibleChunksCoordinates)
        {
            ChunkController controller;

            if (!ActiveChunks.ContainsKey(chunk))
            {
                if (CacheChunks.ContainsKey(chunk))
                {
                    controller = CacheChunks[chunk];
                    this.ActiveChunks.Add(chunk, controller);
                }
                else
                {
                    controller = Factory.CreateChunkController(chunk, Configuration, this.transform, cancellationToken.Token);
                    this.ActiveChunks.Add(chunk, controller);
                }
            }
            else
            {
                controller = ActiveChunks[chunk];
            }

            if (controller == null)
                throw new System.ArgumentNullException("ChunkController does not exist. Was the gameObject deleted?");

            controller.LODIndex = Layout.GetRenderDetail(followerChunkPos, controller.Coordinates);
        }

        IsBusy = false;
    }

    /// <summary>
    /// Checks if the follower has moved far enough since the last chunk update to warrant refreshing.
    /// </summary>
    /// <returns>True if chunks should be updated, false otherwise.</returns>
    private bool IsFollowerOutsideOfRange()
    {
        float viewerDistance = Vector3.Distance(Follower.position, LastKnownFollowerPosition);
        if (viewerDistance > TravelDistanceToUpdateChunks)
        {
            LastKnownFollowerPosition = Follower.position;
            return true;
        }

        return false;
    }
}