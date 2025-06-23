using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using WaveHarmonic.Crest;

[System.Serializable]
public class LODThresholds
{
    [Tooltip("LOD0 — up close: player feet, terrain sculpting, grass")]
    public float LOD0 = 5f;

    [Tooltip("LOD1 — near field: trees, paths")]
    public float LOD1 = 100f;

    [Tooltip("LOD2 — visible terrain shape, some structure")]
    public float LOD2 = 850f;

    [Tooltip("LOD3 — far terrain shape only")]
    public float LOD3 = 1400f;

    [Tooltip("LOD4 — horizon terrain (proxy/shader only)")]
    public float LOD4 = 3000f;

    public float[] ToArray()
    {
        float[] result = new float[5];

        // Step size per LOD (1 << LODIndex) * base chunk size
        int baseChunkSize = 16;

        result[0] = LOD0 * (baseChunkSize << 0); // 16
        result[1] = result[0] + (LOD1 * (baseChunkSize << 1)); // 32
        result[2] = result[1] + (LOD2 * (baseChunkSize << 2)); // 64
        result[3] = result[2] + (LOD3 * (baseChunkSize << 3)); // 128
        result[4] = result[3] + (LOD4 * (baseChunkSize << 4)); // 256

        return result;
    }
}

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    [Header("LOD Settings")]
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

    private CancellationTokenSource cancellationToken;

    private Quaternion lastFollowerRotation;

    private List<ChunkOctTree> rootTrees = new List<ChunkOctTree>();

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

        if (forceActiveUpdate || deltaAngle >= 15f)
        {
            forceActiveUpdate = false;
            foreach (var root in rootTrees)
                root.UpdateActiveStatus(this.chunkServices.Layout.FollowerWorldPosition, frustum);

            this.lastFollowerRotation = chunkManager.Follower.transform.rotation;
        }

        foreach (var root in rootTrees)
        {
            root.Update(this.chunkServices.Layout.FollowerWorldPosition, lodThresholds.ToArray());
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
        cancellationToken.Cancel();
        this.processor.Dipose();
    }

    void OnRenderObject()
    {
        processor.Draw();
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

        this.cancellationToken = new CancellationTokenSource();
        this.chunkManager = this.GetComponent<ChunkManager>();

        this.chunkServices = services;
        this.processor = new ChunkGenerationProcessor(this.chunkServices, this.cancellationToken.Token);

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

        // Create root nodes.
        for (int dx = initialRootRange.X.Min; dx < initialRootRange.X.Max; dx++)
            for (int dy = initialRootRange.Y.Min; dy < initialRootRange.Y.Max; dy++)
                for (int dz = initialRootRange.Z.Min; dz < initialRootRange.Z.Max; dz++)
                    CreateRoot(new Vector3Int(dx, dy, dz), chunkSize, startPos);
    }

    /// <summary>
    /// Create a new root tree and add to the collection.
    /// </summary>
    /// <param name="coord"></param>
    /// <param name="lod"></param>
    /// <param name="offset"></param>
    private void CreateRoot(Vector3Int coord, int chunkSize, Vector3 offset)
    {
        Vector3 chunkWorldPos = offset + new Vector3(
            coord.x * chunkSize,
            coord.y * chunkSize,
            coord.z * chunkSize);

        Bounds bounds = new Bounds(chunkWorldPos + Vector3.one * (chunkSize / 2), Vector3.one * chunkSize);

        var root = new ChunkOctTree(chunkServices, processor, bounds);
        rootTrees.Add(root);
    }
}