using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
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
    /// A collection of tree roots used to make and establish the chunks. These roots
    /// will be used for subdivision when making new chunks.
    /// </summary>
    private List<ChunkQuadTree> RootTrees = new List<ChunkQuadTree>();

    /// <summary>
    /// A collection of active chunks in the game world.
    /// </summary>
    public Dictionary<ChunkContext, ChunkRenderData> Chunks = new Dictionary<ChunkContext, ChunkRenderData>();

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

    float[] lodThresholds = new float[]
    {
    192f,    // LOD0 — very close to player, only fine detail
    368f,    // LOD1 — close-range but less detailed
    748f,   // LOD2 — mid-range terrain silhouette
    996f,   // LOD3 — distant terrain
    1300f,   // LOD4 — very far terrain
    };


    private async void Update()
    {
        this.debugText.text = this.Chunks.Count.ToString() + "\n" +
            this.Renderer.generationQueue.ToString() + "\n" +
            sw.Elapsed.TotalSeconds.ToString();

        this.UpdateLayout();
    }

    private void UpdateLayout()
    {
        Vector3 pos = this.Follower.position;

        this.Services.Layout.Follower = this.Follower;
        this.Services.Layout.FollowerWorldPosition = pos;

        foreach (var root in RootTrees)
        {
            root.Update(pos, lodThresholds);
        }
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

        InitializeRootChunks();
    }

    private void InitializeRootChunks()
    {
        if (!IsInitialized)
            return;

        layoutCts.Cancel();
        layoutCts = new CancellationTokenSource();

        int lod = 4;
        int chunkSize = this.Services.Configuration.DensityOptions.ChunkSize << lod;

        // Use world position (float) instead of grid coord
        Vector3 playerPos = this.Follower.transform.position;

        // Convert to chunk-aligned center
        Vector3 centerOffset = new Vector3(chunkSize / 2f, chunkSize / 2f, chunkSize / 2f);

        // Determine base world position (bottom-left-near corner of 2×2×2 cube)
        Vector3 startPos = playerPos - centerOffset;

        for (int dx = -16; dx <= 16; dx++)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dz = -16; dz <= 16; dz++)
                {
                    Vector3 chunkWorldPos = startPos + new Vector3(
                        dx * chunkSize,
                        dy * chunkSize,
                        dz * chunkSize
                    );

                    Bounds bounds = new Bounds(
                        chunkWorldPos + Vector3.one * (chunkSize / 2f),
                        Vector3.one * chunkSize
                    );

                    var root = new ChunkQuadTree(Services, Renderer, bounds);
                    RootTrees.Add(root);
                }
            }
        }

        Debug.Log("Finished creating LOD5 root chunks.");
    }
}