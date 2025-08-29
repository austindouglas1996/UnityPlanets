using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-facing host for chunk rendering:
/// - Ticks generator + octrees (Update)
/// - Applies finalized job results on the main thread via the processor (LateUpdate)
/// - Triggers drawing (OnRenderObject)
///
/// Architectural seam:
///   This component owns the processor. The processor is the only thing that mutates
///   the render router. We never call the router directly from here.
/// </summary>
public class ChunkRendererMono : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] public bool ShowTerrain = true;
    [HideInInspector] private bool isInitialized = false;
    [SerializeField] private bool LoveTheBoo = true;

    [Header("Shaders")]
    [SerializeField] public ComputeShader MarchingCubes;

    [Header("Generation")]
    [Tooltip("Initial range for LOD4 chunk loading.")]
    [SerializeField]
    private ChunkRenderRange initialRootRange = new ChunkRenderRange(-8, 8, 0, 2, -8, 8);

    private IChunkServices chunkServices;
    private ChunkGenerationProcessor processor;

    private ChunkOctreeService treeMan;
    private ChunkLodTree lodTree;


    // Debug test fields
    private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    private int frameCount = 0;
    private float fpsSum = 0f;
    private int phase = 0; // 0 = with update, 1 = without update
    private float sampleDuration = 60f; // seconds

    /// <summary>
    /// Update the chunk layout and render any available chunks.
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;
        this.chunkServices.Generator.Update();

        // run LOD tree update only in phase 0
        if (phase == 0)
            lodTree.Update();

        // FPS sampling
        frameCount++;
        fpsSum += 1f / Time.unscaledDeltaTime;

        if (stopwatch.Elapsed.TotalSeconds >= sampleDuration)
        {
            float avgFps = fpsSum / frameCount;
            UnityEngine.Debug.Log($"[Phase {phase}] Average FPS over {sampleDuration}s: {avgFps:F2}");

            // reset counters
            frameCount = 0;
            fpsSum = 0f;
            stopwatch.Restart();

            // switch phase
            phase++;
        }
    }

    /// <summary>
    /// IMPORTANT:
    /// IMPORTANT: DO NOT FORGET
    /// IMPORTANT:
    /// IMPORTANT:
    /// IMPORTANT: LateUpdate must be used when rendering chunks.
    /// IMPORTANT:
    /// IMPORTANT:
    /// IMPORTANT:
    /// IMPORTANT: DO NOT FORGET
    /// IMPORTANT:
    /// IMPORTANT:
    /// IMPORTANT:
    /// </summary>
    private void LateUpdate()
    {
        if (!isInitialized) return;

        processor.Update();
    }

    /// <summary>
    /// On cancellation, cancel all jobs and destroy components.
    /// </summary>
    private void OnDisable()
    {
        this.processor.Dispose();
    }

    /// <summary>
    /// Draw the GPU objects.
    /// </summary>
    void OnRenderObject()
    {
        if (ShowTerrain)
            processor.Draw();
    }

    /// <summary>
    /// Draw debug symbols.
    /// </summary>
    private void OnDrawGizmos()
    {

    }

    /// <summary>
    /// Initialize the <see cref="ChunkRendererMono"/> to create initial chunks and start rendering.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="services"></param>
    public void Initialize(IChunkServices services)
    {
        stopwatch.Start();
        this.chunkServices = services;
        this.processor = new ChunkGenerationProcessor(this.chunkServices);

        isInitialized = true;

        this.InitializeRootChunks();
    }

    /// <summary>
    /// Refresh chunks now.
    /// </summary>
    public void RefreshChunks()
    {
        //rootTrees.Clear();
        this.processor.RemoveAll();
        this.InitializeRootChunks();
    }

    /// <summary>
    /// Create the initial root chunks in the world.
    /// </summary>
    private void InitializeRootChunks()
    {
        if (!isInitialized)
            return;

        // Use the highest level LOD.
        int chunkSize = this.chunkServices.Layout.GetChunkSize(4);

        // Use world position (float) instead of grid coord
        Vector3 playerPos = this.chunkServices.Layout.FollowerWorldPosition;

        // Convert to chunk-aligned center
        Vector3 centerOffset = new Vector3(chunkSize / 2f, chunkSize / 2f, chunkSize / 2f);

        // Determine base world position.
        Vector3 startPos = playerPos - centerOffset;

        if (initialRootRange == null)
        {
            Debug.LogError("Initial Root Range is null.");
            throw new System.ArgumentException("Chunk initial loading range is not set.");
        }

        // Create manager.
        lodTree = new ChunkLodTree(this.chunkServices, this.processor);

        // Create root nodes.
        for (int dx = initialRootRange.X.Min; dx < initialRootRange.X.Max; dx++)
            for (int dy = initialRootRange.Y.Min; dy < initialRootRange.Y.Max; dy++)
                for (int dz = initialRootRange.Z.Min; dz < initialRootRange.Z.Max; dz++)
                    CreateRoot(new Vector3Int(dx, dy, dz), 4, startPos);
    }

    /// <summary>
    /// Create a new root tree and add to the collection.
    /// </summary>
    /// <param name="coord"></param>
    /// <param name="lod"></param>
    /// <param name="offset"></param>
    private void CreateRoot(Vector3Int coord, int lodIndex, Vector3 offset)
    {
        Bounds bounds = this.chunkServices.Layout.GetBounds(coord, lodIndex);
        lodTree.AddRoot(bounds);
    }
}