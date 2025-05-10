using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using VHierarchy.Libs;

/// <summary>
/// Manages all active chunks in the world. Handles loading, unloading, re-coloring,
/// and modifying terrain based on player movement and brush interactions.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    /// <summary>
    /// How far the follower has to move before we trigger an update of active chunks.
    /// </summary>
    [Header("Rendering"), Tooltip("How far the follower needs to be travel before we update the active chunks.")]
    public float TravelDistanceToUpdateChunks = 10f;

    [Tooltip("Should chunks the follower cannot see be automatically hidden?")]
    public bool AutomaticallyHideChunksOutOfView = true;

    /// <summary>
    /// The transform that this chunk system follows, like the player.
    /// </summary>
    [HideInInspector] public Transform Follower;

    /// <summary>
    /// Chunk configurations.
    /// </summary>
    [SerializeField] private IChunkConfiguration Configuration;
    [SerializeField] private IChunkLayout Layout;
    [SerializeField] private IChunkControllerFactory Factory;
    [SerializeField] private IChunkGenerator Generator;
    [SerializeField] private ChunkGenerationQueue GenerationQueue;

    /// <summary>
    /// A cancellation token used to help with cancelling processes on game close.
    /// </summary>
    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    /// <summary>
    /// A collection of active chunks in the game world.
    /// </summary>
    private Dictionary<Vector3Int, ChunkController> Chunks = new Dictionary<Vector3Int, ChunkController>();

    /// <summary>
    /// Tells whether the manager is currently busy executing other tasks.
    /// </summary>
    private bool IsBusy = false;

    /// <summary>
    /// Returns whether <see cref="Initialize(IChunkConfiguration, IChunkLayout, IChunkControllerFactory)"/> has been successful.
    /// </summary>
    private bool IsInitialized = false;

    private Quaternion LastFollowerRotation;

    private async void Update()
    {
        if (!IsBusy && Layout.ShouldUpdateLayout(Follower.position))
        {
            await UpdateChunks();
        }

        if (AutomaticallyHideChunksOutOfView)
        {
            Quaternion currentRot = Follower.transform.rotation;
            float angleDelta = Quaternion.Angle(LastFollowerRotation, currentRot);

            if (angleDelta > 30f)
            {
                LastFollowerRotation = currentRot;
                UpdateChunkVisibility();
            }
        }
    }

    private void OnDisable()
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
        this.Generator = this.Factory.CreateGenerator();

        if (this.Generator == null)
            throw new System.ArgumentNullException("Generator is null.");

        this.GenerationQueue = new ChunkGenerationQueue(this.Follower, this.Generator, this.Configuration, this.cancellationToken.Token);

        this.IsInitialized = true;
    }

    /// <summary>
    /// Request a chunk be generated based on a <see cref="ChunkController"/> data.
    /// </summary>
    /// <param name="controller"></param>
    public void RequestNewChunkGeneration(ChunkController controller)
    {
        var task = this.GenerationQueue.RequestChunkGeneration(controller.Coordinates, controller.LODIndex);
        task.ContinueWith(t => 
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            Mesh mesh = this.Generator.GenerateMesh(t.Result, this.Configuration);
            controller.UpdateChunkData(t.Result, mesh);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Request a chunk be updated due to player modifications.
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="brush"></param>
    /// <param name="isAdding"></param>
    public void RequestChunkModification(ChunkController controller, TerrainBrush brush, bool isAdding)
    {
        ChunkModificationJob modificationJob = new ChunkModificationJob(controller.ChunkData[0], brush, isAdding);
        var task = this.GenerationQueue.RequestChunkGeneration(controller.Coordinates, 0, modificationJob);

        task.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            Mesh mesh = this.Generator.GenerateMesh(t.Result, this.Configuration);
            controller.UpdateChunkData(t.Result, mesh);
        }, TaskScheduler.FromCurrentSynchronizationContext());
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
    /// Modifies all chunks that intersect the brush area.
    /// Used when the player adds or removes terrain.
    /// </summary>
    /// <param name="brush">The terrain brush to apply.</param>
    /// <param name="isAdding">True to add terrain, false to remove.</param>
    /// <param name="bufferMultiplier">Optional chunk bounds buffer.</param>
    /// <param name="token">Optional cancellation token.</param>
    public void ModifyTerrain(TerrainBrush brush, bool isAdding, float bufferMultiplier = 0.5f, CancellationToken token = default)
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

                    if (Layout.PreviousActiveChunks.TryGetValue(neighborCoord, out var chunk))
                    {
                        ChunkController controller = Chunks[chunk];
                        Bounds chunkBounds = new Bounds(controller.transform.position + chunkSize * bufferMultiplier, chunkSize);

                        if (brushBounds.Intersects(chunkBounds))
                        {
                            RequestChunkModification(controller, brush, isAdding);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rebuilds the list of active chunks based on the follower’s current position.
    /// Adds new chunks and removes out-of-range ones.
    /// </summary>
    private async Task UpdateChunks()
    {
        if (IsBusy || !IsInitialized)
            return;
        IsBusy = true;

        Stopwatch sw = new Stopwatch();
        sw.Start();

        int active = 0;
        int created = 0;
        int remove = 0;

        await foreach (var entry in Layout.StreamChunkLayoutUpdate(Follower.position))
        {
            await Task.Yield();

            // Is this chunk no longer active?
            if (entry.IsStale)
            {
                this.GenerationQueue.CancelChunkGeneration(entry.Coordinates);
                remove++;
                continue;
            }

            // Does this chunk already exist?
            if (Chunks.TryGetValue(entry.Coordinates, out var newChunk))
            {
                newChunk.LODIndex = entry.LOD;
            }
            else
            {
                ChunkController newController = Factory.CreateChunkController
                    (entry.Coordinates, this, Configuration, this.transform, cancellationToken.Token);
                newController.LODIndex = entry.LOD;

                Chunks[entry.Coordinates] = newController;
                created++;
            }

            active++;
        }

        this.IsBusy = false;
        sw.Stop();

        UnityEngine.Debug.Log($"Created Chunks: {created}, Active Chunks: {active}, removed chunks {remove}, time {sw.ElapsedMilliseconds}MS");
    }

    /// <summary>
    /// Update the chunk visibility based on what the follower can see.
    /// </summary>
    private void UpdateChunkVisibility()
    {
        Vector3 camForward = Follower.transform.forward;

        foreach (var chunk in Chunks)
        {
            Vector3 chunkCenter = chunk.Value.transform.position + new Vector3(this.Configuration.ChunkSize, this.Configuration.ChunkSize, this.Configuration.ChunkSize) * 0.5f;
            Vector3 toChunk = (chunkCenter - Follower.transform.position);

            // Always render closeup chunks.
            if (toChunk.magnitude < 40f)
            {
                chunk.Value.gameObject.SetActive(true);
                continue;
            }

            float dot = Vector3.Dot(camForward, toChunk.normalized);
            bool isRoughlyInFront = dot > 0f;
            chunk.Value.gameObject.SetActive(isRoughlyInFront);
        }
    }
}