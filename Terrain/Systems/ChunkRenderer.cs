using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    [Tooltip("Should chunks the follower cannot see be automatically hidden?")]
    public bool AutomaticallyHideChunksOutOfView = true;

    private ChunkManager chunkManager;
    private IChunkServices chunkServices;
    public ChunkGenerationQueue generationQueue;

    private CancellationTokenSource cancellationToken;

    private Quaternion LastFollowerRotation;

    public bool isInitialized = false;

    private void LateUpdate()
    {
        if (!isInitialized)
        {
            return;
        }

        foreach (var chunk in this.chunkManager.Chunks.Values)
        {
            if (chunk.RenderType == ChunkRenderType.GPU && chunk.IsActive)
            {
                Graphics.DrawMesh(chunk.Mesh, chunk.LocalToWorld, material, 0);
            }
        }

        if (AutomaticallyHideChunksOutOfView)
        {
            Quaternion currentRot = chunkManager.Follower.transform.rotation;
            float angleDelta = Quaternion.Angle(LastFollowerRotation, currentRot);

            if (angleDelta > 30f)
            {
                LastFollowerRotation = currentRot;
                UpdateVisibility();
            }
        }
    }

    private Material material;

    public void Initialize(ChunkManager manager, IChunkServices services)
    {
        material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        material.SetFloat("_Smoothness", 0f);

        this.cancellationToken = new CancellationTokenSource();
        this.chunkManager = this.GetComponent<ChunkManager>();

        this.generationQueue = new ChunkGenerationQueue(services, cancellationToken.Token);

        isInitialized = true;
    }

    /// <summary>
    /// Request a chunk be updated due to player modifications.
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="brush"></param>
    /// <param name="isAdding"></param>
    public void RequestModification(ChunkController controller, TerrainBrush brush, bool isAdding)
    {
        throw new System.NotImplementedException("ChunkModificationJob needs chunkData back.");
        ChunkModificationJob modificationJob = new ChunkModificationJob(null, brush, isAdding);
        var task = this.generationQueue.RequestChunkModification(controller.Coordinates, 0, modificationJob);

        task.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            ChunkRenderData renderData = new ChunkRenderData(controller.Coordinates, t.Result, controller.transform.localToWorldMatrix);

            SubmitExistingChunk(renderData);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Remove a chunk from all jobs and collections.
    /// </summary>
    /// <param name="coordinate"></param>
    public void RemoveChunk(Vector3Int coordinate)
    {
        if (this.chunkManager.Chunks.TryGetValue(coordinate, out var chunk))
        {
            if (chunk.Controller != null)
                this.chunkServices.ControllerFactory.Release(chunk.Controller);

            this.generationQueue.CancelChunkGeneration(coordinate);
        }
    }

    /// <summary>
    /// Request a chunk be generated based on a <see cref="ChunkController"/> data.
    /// </summary>
    /// <param name="controller"></param>
    public void RequestGeneration(Vector3Int coordinates, int lodIndex)
    {
        var task = this.generationQueue.RequestChunkGeneration(coordinates, lodIndex);
        task.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            Vector3 worldPos = this.chunkServices.Layout.ToWorld(coordinates, lodIndex);
            Matrix4x4 transform = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one);

            // Generate mesh and apply color.
            ChunkRenderData renderData = new ChunkRenderData(coordinates, t.Result, transform);

            this.SubmitNewChunk(renderData);

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Submit a chunk to be rendered into the world.
    /// </summary>
    /// <param name="chunkRenderData"></param>
    private void SubmitNewChunk(ChunkRenderData chunkRenderData)
    {
        var coord = chunkRenderData.Coordinates;
        var controller = this.chunkServices.ControllerFactory.CreateChunkController(coord, chunkRenderData.LOD, this.cancellationToken.Token);
        chunkRenderData.Controller = controller;
        chunkRenderData.RenderType = ChunkRenderType.GameObject;
        chunkManager.Chunks[coord] = chunkRenderData;
        controller.ApplyChunkData(chunkRenderData);

        /*
        if (chunkRenderData.LOD == 0)
        {
            var controller = chunkManager.Factory.CreateChunkController(coord, this.cancellationToken.Token);
            chunkRenderData.Controller = controller;
            chunkRenderData.RenderType = ChunkRenderType.GameObject;
            chunkManager.Chunks[coord] = chunkRenderData;
            controller.ApplyChunkData(chunkRenderData);
        }
        else
        {
            chunkRenderData.RenderType = ChunkRenderType.GPU;
            chunkRenderData.Controller = null;
        }*/
        
        chunkManager.Chunks[coord] = chunkRenderData;
    }

    private void SubmitExistingChunk(ChunkRenderData chunkRenderData)
    {
        var coord = chunkRenderData.Coordinates;
        int lod = chunkRenderData.LOD;

        if (this.chunkManager.Chunks.TryGetValue(chunkRenderData.Coordinates, out var existing))
        {
            bool ExistingIsGO = existing.RenderType == ChunkRenderType.GameObject;

            // GPU to GO.
            if (!ExistingIsGO && lod == 0)
            {
                var controller = this.chunkServices.ControllerFactory.CreateChunkController(coord, lod, this.cancellationToken.Token);
                chunkRenderData.Controller = controller;
                chunkRenderData.RenderType = ChunkRenderType.GameObject;

                controller.ApplyChunkData(chunkRenderData);
            }
            // GO to GPU
            else if (ExistingIsGO && lod > 0)
            {
                if (existing.Controller != null)
                    this.chunkServices.ControllerFactory.Release(existing.Controller);

                chunkRenderData.RenderType = ChunkRenderType.GPU;
                chunkRenderData.Controller = null;
            }
            // GO to GO (Update)
            else if (ExistingIsGO && lod == 0)
            {
                if (existing.Controller != null)
                    existing.Controller.ApplyChunkData(chunkRenderData);
            }
            // GPU to GPU (Update)
            else if (!ExistingIsGO && lod > 0)
            {
                chunkRenderData.RenderType = ChunkRenderType.GPU;
            }
        }
    }

    /// <summary>
    /// Update the chunk visibility of each chunk.
    /// </summary>
    private void UpdateVisibility()
    {
        Vector3 camForward = chunkManager.Follower.transform.forward;

        foreach (var chunk in this.chunkManager.Chunks)
        {
            Vector3 size = this.chunkServices.Configuration.DensityOptions.ChunkSize3;
            Vector3 chunkCenter = chunk.Value.LocalToWorld.GetPosition() + size * 0.5f;
            Vector3 toChunk = (chunkCenter - chunkManager.Follower.transform.position);

            // Always render closeup chunks.
            if (toChunk.magnitude < 40f)
            {
                chunk.Value.IsActive = true;
                continue;
            }

            float dot = Vector3.Dot(camForward, toChunk.normalized);
            bool isRoughlyInFront = dot > 0f;
            chunk.Value.IsActive = isRoughlyInFront;
        }
    }
}