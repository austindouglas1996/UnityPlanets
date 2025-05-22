using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

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

    private IChunkServices Services;
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
        System.GC.Collect();
        Resources.UnloadUnusedAssets();

        // Create Canvas
        GameObject canvasObj = new GameObject("RuntimeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Attach to camera so it moves with it
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            canvasObj.transform.SetParent(mainCamera.transform);
            canvasObj.transform.localPosition = new Vector3(-10, -5, 20); // 2 units in front of camera
            canvasObj.transform.localRotation = Quaternion.identity;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(10, 10); // size of the canvas in world units

        // Create Text object
        GameObject textObj = new GameObject("ChunkText", typeof(RectTransform));
        textObj.transform.SetParent(canvasObj.transform, false);

        debugText = textObj.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 1; // smaller font size for world space
        debugText.color = Color.white;
        debugText.alignment = TextAlignmentOptions.Center;
        debugText.text = "Active Chunks: 0";

        RectTransform rectTransform = debugText.rectTransform;
        rectTransform.sizeDelta = new Vector2(50, 80);
        rectTransform.anchoredPosition = Vector2.zero;

        sw = new System.Diagnostics.Stopwatch();
        sw.Start();
    }

    public float timeStop = 20f;
    private System.Diagnostics.Stopwatch sw;

    private async void Update()
    {
        this.debugText.text = this.Chunks.Count.ToString() + "\n" +
            sw.Elapsed.TotalSeconds.ToString();

        this.UpdateLayout();

        if (this.Services.Layout.ShouldUpdateLayout())
        {
            await UpdateChunks();
        }
    }

    private void UpdateLayout()
    {
        this.Services.Layout.Follower = this.Follower;
        this.Services.Layout.FollowerWorldPosition = this.Follower.position;
    }

    private void OnDisable()
    {
        cancellationToken.Cancel();
    }

    private void Awake()
    {
        this.Renderer = this.GetComponent<ChunkRenderer>();
    }

    /// <summary>
    /// Sets up the chunk manager with the required configuration, layout, and factory.
    /// </summary>
    /// <param name="configuration">Settings for chunk size and behavior.</param>
    /// <param name="layout">Logic to determine visible chunk positions.</param>
    /// <param name="factory">Factory that builds new chunk controllers.</param>
    /// <exception cref="System.ArgumentNullException">If any required dependency is missing.</exception>
    public void Initialize(Transform follower, IChunkServices services)
    {
        this.Follower = follower;
        this.Services = services;

        this.Renderer.Initialize(this, this.Services);

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
        Vector3 chunkSize = this.Services.Configuration.DensityOptions.ChunkSize3;

        Vector3Int hitPosCoord = this.Services.Layout.ToCoordinates(brush.WorldHitPoint, 0);

        // Check all neighbors in a 3x3x3 cube around the hit position
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    token.ThrowIfCancellationRequested();

                    Vector3Int neighborCoord = hitPosCoord + new Vector3Int(x, y, z);

                    if (this.Services.Layout.PreviousActiveChunks.Contains(neighborCoord))
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

    private async Task UpdateChunks()
    {
        if (!IsInitialized)
            return;

        layoutCts.Cancel();
        layoutCts = new CancellationTokenSource();

        int chunks = 0;
        Vector3Int playerCoord = this.Services.Layout.FollowerCoordinates;

        int previousMaxRange = 0;

        for (int lod = 0; lod <= 5; lod++)
        {
            int chunkSize = this.Services.Configuration.DensityOptions.ChunkSize << lod;
            int range = GetRangeForLOD(lod);
            if (range == 0)
                continue;

            Vector3Int center = new Vector3Int(
                playerCoord.x >> lod,
                playerCoord.y >> lod,
                playerCoord.z >> lod
            );

            int minRange = Mathf.Max(0, previousMaxRange - 2);
            int maxRange = previousMaxRange + range; // Inclusive
            previousMaxRange = maxRange;

            for (int x = -maxRange; x <= maxRange; x++)
            {
                for (int z = -maxRange; z <= maxRange; z++)
                {
 
                    for (int y = -10; y <= 25; y++)
                    {
                        if (chunks > 150)
                        {
                            await Task.Yield();
                            chunks = 0;
                        }

                        Vector3Int coord = center + new Vector3Int(x, y, z);
                        ChunkContext newContext = new ChunkContext(coord, lod, this.Services);
                        Renderer.RequestGeneration(newContext);
                        chunks++;
                    }
                }
            }
        }

        Debug.Log("Finished layout.");
    }



    private int GetRangeForLOD(int lod)
    {
        switch (lod)
        {
            case 0: return 64; // High detail near player
            case 1: return 2;
            case 2: return 2;
            case 3: return 2;
            case 4: return 1;
            case 5: return 1;
            default: return 0;
        }
    }

}