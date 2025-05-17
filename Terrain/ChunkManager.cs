using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
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

    /// <summary>
    /// The transform that this chunk system follows, like the player.
    /// </summary>
    [HideInInspector] public Transform Follower;

    /// <summary>
    /// Chunk configurations.
    /// </summary>
    public IChunkConfiguration Configuration;
    public IChunkColorizer Colorizer;
    public IChunkLayout Layout;
    public IChunkControllerFactory Factory;
    public IChunkGenerator Generator;
    private ChunkRenderer Renderer;

    /// <summary>
    /// A cancellation token used to help with cancelling processes on game close.
    /// </summary>
    private CancellationTokenSource cancellationToken = new CancellationTokenSource();
    private CancellationTokenSource layoutCts = new CancellationTokenSource();

    /// <summary>
    /// A collection of active chunks in the game world.
    /// </summary>
    public Dictionary<Vector3Int, ChunkRenderData> Chunks = new Dictionary<Vector3Int, ChunkRenderData>();

    /// <summary>
    /// Returns whether <see cref="Initialize(IChunkConfiguration, IChunkLayout, IChunkControllerFactory)"/> has been successful.
    /// </summary>
    private bool IsInitialized = false;

    private TextMeshProUGUI debugText;

    private void Start()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("RuntimeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Attach to camera so it moves with it
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            canvasObj.transform.SetParent(mainCamera.transform);
            canvasObj.transform.localPosition = new Vector3(0, 0, 20); // 2 units in front of camera
            canvasObj.transform.localRotation = Quaternion.identity;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(10, 10); // size of the canvas in world units

        // Create Text object
        GameObject textObj = new GameObject("ChunkText", typeof(RectTransform));
        textObj.transform.SetParent(canvasObj.transform, false);

        debugText = textObj.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 2; // smaller font size for world space
        debugText.color = Color.white;
        debugText.alignment = TextAlignmentOptions.Center;
        debugText.text = "Active Chunks: 0";

        RectTransform rectTransform = debugText.rectTransform;
        rectTransform.sizeDelta = new Vector2(50, 80);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private float firstChunk = -1;

    private Queue<int> queueHistory = new Queue<int>();
    private const int maxQueueHistory = 30; // Store last 30 frames
    public float timeStop = 20f;


    private async void Update()
    {
        if (Time.time < timeStop)
        {
            if (firstChunk == -1 && Chunks.Count > 0)
                firstChunk = Time.time;

            int currentQueueCount = this.Renderer.generationQueue.GetQueueCount;
            queueHistory.Enqueue(currentQueueCount);

            if (queueHistory.Count > maxQueueHistory)
                queueHistory.Dequeue();

            int maxQueue = 0, sumQueue = 0;
            foreach (var count in queueHistory)
            {
                sumQueue += count;
                if (count > maxQueue) maxQueue = count;
            }
            float avgQueue = (float)sumQueue / queueHistory.Count;

            debugText.text = $"" +
                $"Active Chunks: {Chunks.Count}\n" +
                $"First Chunk: {firstChunk:F1} sec\n" +
                $"Total Time: {Time.time:F1} sec\n" +
                $"Queue: {currentQueueCount} (avg: {avgQueue:F1}, max: {maxQueue})";
        }

        this.UpdateLayout();

        if (Layout.ShouldUpdateLayout())
        {
            await UpdateChunks();
        }
    }

    private void UpdateLayout()
    {
        this.Layout.Follower = this.Follower;
        this.Layout.FollowerWorldPosition = this.Follower.position;
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
    public void Initialize(Transform follower, IChunkConfiguration configuration, IChunkLayout layout, IChunkColorizer colorizer, IChunkControllerFactory factory, IChunkGenerator generator)
    {
        if (configuration == null)
            throw new System.ArgumentNullException("Configuration is null.");
        if (layout == null)
            throw new System.ArgumentNullException("Layout is null.");
        if (colorizer == null)
            throw new System.ArgumentNullException("Color is null.");
        if (factory == null)
            throw new System.ArgumentNullException("Factory is null.");
        if (generator == null)
            throw new System.ArgumentNullException("Generator is null.");

        this.Follower = follower;

        this.Configuration = configuration;
        this.Layout = layout;
        this.Colorizer = colorizer;
        this.Factory = factory;
        this.Generator = generator;

        this.Renderer = this.GetComponent<ChunkRenderer>();
        this.Renderer.Initialize(factory);

        this.IsInitialized = true;
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

        Vector3Int hitPosCoord = Layout.ToCoordinates(brush.WorldHitPoint);

        // Check all neighbors in a 3x3x3 cube around the hit position
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    token.ThrowIfCancellationRequested();

                    Vector3Int neighborCoord = hitPosCoord + new Vector3Int(x, y, z);

                    if (Layout.PreviousActiveChunks.Contains(neighborCoord))
                    {
                        ChunkRenderData chunk = this.Chunks[neighborCoord];

                        ChunkController controller = chunk.Controller;
                        Bounds chunkBounds = new Bounds(controller.transform.position + chunkSize * bufferMultiplier, chunkSize);

                        if (brushBounds.Intersects(chunkBounds))
                        {
                            Renderer.RequestModification(controller, brush, isAdding);
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
        if (!IsInitialized)
            return;

        layoutCts.Cancel();
        layoutCts = new CancellationTokenSource();

        var bounds = Layout.GetActiveChunksAroundFollower();
        var chunkPositions = new List<Vector3Int>();

        int chunks = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            chunks++;
            if (chunks > 750)
            {
                await Task.Yield();
                chunks = 0;
            }

            Renderer.RequestGeneration(pos, Layout.GetRenderDetail(pos));
        }

        Debug.Log("Finished layout.");
    }
}