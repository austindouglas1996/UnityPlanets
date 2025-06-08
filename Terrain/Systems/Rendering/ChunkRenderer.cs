using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
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
    public float LOD2 = 450f;

    [Tooltip("LOD3 — far terrain shape only")]
    public float LOD3 = 700f;

    [Tooltip("LOD4 — horizon terrain (proxy/shader only)")]
    public float LOD4 = 1000f;

    public float[] ToArray() => new[] { LOD0, LOD1, LOD2, LOD3, LOD4 };
}

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    [Header("LOD Settings")]
    [SerializeField]
    private LODThresholds lodThresholds = new();

    [Header("Rendering")]
    [Tooltip("Base material used on chunk instances")]
    [SerializeField]
    private Material material;

    [Tooltip("Show chunk region layouts.")]
    [SerializeField]
    private bool showRootGizmos = false;

    [Tooltip("Initial range for LOD4 chunk loading.")]
    [SerializeField]
    private ChunkRenderRange initialRootRange = new ChunkRenderRange(-8, 8, 0, 2, -8, 8);

    [Tooltip("How far the follower needs to travel before we update active chunks.")]
    [SerializeField]
    public float travelDistanceToUpdateChunks = 10f;

    [HideInInspector] private bool isInitialized = false;

    private ChunkManager chunkManager;
    private IChunkServices chunkServices;

    private MeshBatchDrawer meshDrawer;
    private FoliageGenerator foliageGenerator;
    private ChunkGenerationProcessor generationQueue;
    private CancellationTokenSource cancellationToken;

    private Quaternion lastFollowerRotation;

    private List<ChunkOctTree> rootTrees = new List<ChunkOctTree>();
    private Dictionary<ChunkContext, ChunkRenderData> chunks = new Dictionary<ChunkContext, ChunkRenderData>();

    /// <summary>
    /// Update the chunk layout and render any available chunks.
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        this.meshDrawer.Update();
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

        if (forceActiveUpdate)
        {
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            forceActiveUpdate = false;
            foreach (var root in rootTrees)
                root.UpdateActiveStatus(this.chunkServices.Layout.FollowerWorldPosition, frustum);
        }

        float deltaAngle = Quaternion.Angle(lastFollowerRotation, chunkManager.Follower.transform.rotation);
        if (deltaAngle >= 15f)
        {
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(Camera.main);

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

        /*
         * 
         * 
         * 
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * THIS IS AN AWFUL WAY TO DO THIS
         * 
         * 
         * 
         */
        foreach (var chunk in this.chunks.Values)
        {
            if (chunk.CanRenderGPU)
                Graphics.DrawMesh(chunk.Mesh, chunk.LocalToWorld, material, 0);
        }
    }

    /// <summary>
    /// Debug options for displaying chunk bounds.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showRootGizmos) return;
        foreach (var root in rootTrees)
        {
            Gizmos.DrawWireCube(root.Bounds.center, root.Bounds.size);
        }
    }

    /// <summary>
    /// On cancellation, cancel all jobs and destroy components.
    /// </summary>
    private void OnDisable()
    {
        cancellationToken.Cancel();
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

        material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        material.SetFloat("_Smoothness", 0f);

        this.cancellationToken = new CancellationTokenSource();
        this.chunkManager = this.GetComponent<ChunkManager>();

        this.meshDrawer = new MeshBatchDrawer(Camera.main);

        this.chunkServices = services;
        this.generationQueue = new ChunkGenerationProcessor(services, cancellationToken.Token);
        this.foliageGenerator = new FoliageGenerator();

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
    /// Request a chunk be generated based on a <see cref="ChunkController"/> data.
    /// </summary>
    /// <param name="controller"></param>
    public void RequestGeneration(ChunkContext context, ChunkOctTree quadNode = null)
    {
        var task = this.generationQueue.RequestChunkGeneration(context);
        task.ContinueWith(t =>
        {
            try
            {
                if (t.Status != TaskStatus.RanToCompletion
                || t.Result == null
                || t.Result.MeshData == null
                || t.Result.MeshData.IsEmpty
                || !t.Result.MeshData.IsRenderable)
                {
                    // We still return a value here so the 
                    // tree does not keep waiting for a child that is never
                    // going to come ):
                    quadNode.SetRenderData(context.Coordinates, null);
                    return;
                }

                // Generate mesh and apply color.
                ChunkRenderData renderData = new ChunkRenderData(context, t.Result, context.Transform);
                renderData.Tree = quadNode;

                this.SubmitNewChunk(renderData);

                if (quadNode != null)
                {
                    quadNode.SetRenderData(context.Coordinates, renderData);
                }
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Remove a chunk from all jobs and collections.
    /// </summary>
    /// <param name="renderData"></param>
    public void RemoveChunk(ChunkRenderData renderData)
    {
        if (renderData == null)
        {
            return;
        }

        this.generationQueue.CancelChunkGeneration(renderData.Coordinates, renderData.LOD);

        if (renderData.State == ChunkRenderState.GameObject && renderData.Controller != null)
        {
            this.chunkServices.ControllerFactory.Release(renderData.Controller);
            renderData.SetController(null);
        }

        this.meshDrawer.Remove(renderData.Coordinates);

        this.chunks.Remove(renderData.Context);
    }

    /// <summary>
    /// Submit a chunk to be rendered into the world.
    /// </summary>
    /// <param name="chunkRenderData"></param>
    private void SubmitNewChunk(ChunkRenderData chunkRenderData)
    {
        try
        {
            var coord = chunkRenderData.Coordinates;
            if (chunkRenderData.LOD == 0)
            {
                var controller = chunkServices.ControllerFactory.CreateChunkController(chunkRenderData.Context, this.cancellationToken.Token);
                chunkRenderData.SetController(controller);
                chunks[chunkRenderData.Context] = chunkRenderData;
                controller.ApplyChunkData(chunkRenderData);
            }

            if (chunkRenderData.LOD < 2)
            {
                //this.foliageGenerator.ApplyMap(chunkRenderData, chunkRenderData.Context.Transform, this.cancellationToken.Token);

            }

            chunks[chunkRenderData.Context] = chunkRenderData;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
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

        var root = new ChunkOctTree(chunkServices, this, bounds);
        rootTrees.Add(root);
    }
}