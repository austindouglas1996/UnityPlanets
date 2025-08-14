using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    [Header("LOD Settings (Additive)")]
    [SerializeField]
    private LODThresholds lodThresholds = new();

    [Tooltip("Initial range for LOD4 chunk loading.")]
    [SerializeField]
    private ChunkRenderRange initialRootRange = new ChunkRenderRange(-8, 8, 0, 2, -8, 8);

    [Tooltip("How far the follower needs to travel before we update active chunks.")]
    [SerializeField]
    public float travelDistanceToUpdateChunks = 10f;

    [HideInInspector] private bool isInitialized = false;

    private ChunkManager chunkManager;
    private IChunkServices chunkServices;
    private ChunkGenerationProcessor processor;

    private Quaternion lastFollowerRotation;

    private List<ChunkOctTreeNode> rootTrees = new List<ChunkOctTreeNode>();

    /// <summary>
    /// Update the chunk layout and render any available chunks.
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;
        this.UpdateLayout();
    }

    [SerializeField] private bool forceActiveUpdate = false;

    /// <summary>
    /// Update the chunk layout adding or removing any invalid chunks.
    /// </summary>
    private void UpdateLayout()
    {
        if (Time.frameCount % 5 != 0)
            return;

        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        float deltaAngle = Quaternion.Angle(lastFollowerRotation, chunkManager.Follower.transform.rotation);

        foreach (var root in rootTrees)
        {
            root.Tick();
        }
    }

    /// <summary>
    /// IMPORTANT:
    /// LateUpdate must be used when rendering chunk values.
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

    void OnRenderObject()
    {
        if (chunkManager.ShowTerrain)
            processor.Draw();
    }

    private void OnDrawGizmos()
    {
        foreach (var root in rootTrees)
        {
            root.DrawDebugGizmo();
        }
    }

    /// <summary>
    /// Initialize the <see cref="ChunkRenderer"/> to create initial chunks and start rendering.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="services"></param>
    public void Initialize(ChunkManager manager, IChunkServices services)
    {
        System.GC.Collect();
        Resources.UnloadUnusedAssets();

        this.chunkManager = this.GetComponent<ChunkManager>();

        this.chunkServices = services;
        this.processor = new ChunkGenerationProcessor(this.chunkServices, new ChunkRenderLayers(this.chunkServices.Generator, 128,64,32,16));

        isInitialized = true;

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
        ChunkOctTreeMan tree = new ChunkOctTreeMan(this.chunkServices, this.processor, lodThresholds.ToArray());

        // Create root nodes.
        for (int dx = initialRootRange.X.Min; dx < initialRootRange.X.Max; dx++)
            for (int dy = initialRootRange.Y.Min; dy < initialRootRange.Y.Max; dy++)
                for (int dz = initialRootRange.Z.Min; dz < initialRootRange.Z.Max; dz++)
                    CreateRoot(tree, new Vector3Int(dx, dy, dz), chunkSize, startPos);
    }

    /// <summary>
    /// Create a new root tree and add to the collection.
    /// </summary>
    /// <param name="coord"></param>
    /// <param name="lod"></param>
    /// <param name="offset"></param>
    private void CreateRoot(ChunkOctTreeMan tree, Vector3Int coord, int chunkSize, Vector3 offset)
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