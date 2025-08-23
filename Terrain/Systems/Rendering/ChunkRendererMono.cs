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
    [SerializeField] private bool forceActiveUpdate = false;
    [SerializeField] public bool ShowTerrain = true;
    [HideInInspector] private bool isInitialized = false;

    [Header("Shaders")]
    [SerializeField] public ComputeShader MarchingCubes;

    [Header("Generation")]
    [Tooltip("Initial range for LOD4 chunk loading.")]
    [SerializeField]
    private ChunkRenderRange initialRootRange = new ChunkRenderRange(-8, 8, 0, 2, -8, 8);

    private IChunkServices chunkServices;
    private ChunkGenerationProcessor processor;

    private ChunkOctreeService treeMan;
    private List<ChunkOctTreeNode> rootTrees = new List<ChunkOctTreeNode>();

    /// <summary>
    /// Update the chunk layout and render any available chunks.
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;
        this.chunkServices.Generator.Update();

        if (Time.frameCount % 5 != 0)
            return;

        foreach (var root in rootTrees)
        {
            root.Tick();
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
        foreach (var root in rootTrees)
        {
            root.DrawDebugGizmo();
        }
    }

    /// <summary>
    /// Initialize the <see cref="ChunkRendererMono"/> to create initial chunks and start rendering.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="services"></param>
    public void Initialize(IChunkServices services)
    {
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
        rootTrees.Clear();
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
        treeMan = new ChunkOctreeService(this.chunkServices, this.processor);

        // Create root nodes.
        for (int dx = initialRootRange.X.Min; dx < initialRootRange.X.Max; dx++)
            for (int dy = initialRootRange.Y.Min; dy < initialRootRange.Y.Max; dy++)
                for (int dz = initialRootRange.Z.Min; dz < initialRootRange.Z.Max; dz++)
                    CreateRoot(treeMan, new Vector3Int(dx, dy, dz), chunkSize, startPos);
    }

    /// <summary>
    /// Create a new root tree and add to the collection.
    /// </summary>
    /// <param name="coord"></param>
    /// <param name="lod"></param>
    /// <param name="offset"></param>
    private void CreateRoot(ChunkOctreeService tree, Vector3Int coord, int chunkSize, Vector3 offset)
    {
        Vector3 chunkWorldPos = offset + new Vector3(
            coord.x * chunkSize,
            coord.y * chunkSize,
            coord.z * chunkSize);

        Bounds bounds = new Bounds(chunkWorldPos + Vector3.one * (chunkSize / 2), Vector3.one * chunkSize);

        var root = new ChunkOctTreeNode(tree, bounds);
        rootTrees.Add(root);
    }
}