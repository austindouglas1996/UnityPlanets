using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    [Tooltip("Should chunks the follower cannot see be automatically hidden?")]
    public bool AutomaticallyHideChunksOutOfView = true;

    private ChunkManager chunkManager;
    private ChunkGenerationQueue generationQueue;

    private CancellationTokenSource cancellationToken;

    private Quaternion LastFollowerRotation;

    private void LateUpdate()
    {
        foreach (var chunk in this.chunkManager.Chunks.Values)
        {
            if (chunk.RenderType == ChunkRenderType.GPU && chunk.IsActive)
            {
                //Graphics.DrawMesh(chunk.Mesh, chunk.LocalToWorld, material, 0);
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

    public void Initialize(IChunkControllerFactory factory)
    {
        material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        material.SetFloat("_Smoothness", 0f);

        this.cancellationToken = new CancellationTokenSource();
        this.chunkManager = this.GetComponent<ChunkManager>();

        this.generationQueue = new ChunkGenerationQueue(chunkManager.Follower, this.chunkManager.Generator, this.chunkManager.Configuration, cancellationToken.Token);
    }

    public void UpdateOrRequestChunk(Vector3Int coordinate, int lodIndex)
    {
        if (this.chunkManager.Chunks.TryGetValue(coordinate, out var chunk))
        {
            // Same LOD, no changes.
            if (chunk.LOD == lodIndex)
            {
                this.SubmitChunk(chunk);
                return;
            }
        }

        RequestGeneration(coordinate, lodIndex);
    }

    /// <summary>
    /// Request a chunk be updated due to player modifications.
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="brush"></param>
    /// <param name="isAdding"></param>
    public void RequestModification(ChunkController controller, TerrainBrush brush, bool isAdding)
    {
        ChunkModificationJob modificationJob = new ChunkModificationJob(controller.ChunkData[0].Data, brush, isAdding);
        var task = this.generationQueue.RequestChunkModification(controller.Coordinates, 0, modificationJob);

        task.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            Mesh mesh = this.chunkManager.Generator.GenerateMesh(t.Result, this.chunkManager.Configuration);
            ChunkRenderData renderData = new ChunkRenderData(controller.Coordinates, t.Result, mesh, controller.transform.localToWorldMatrix);

            SubmitChunk(renderData);
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
                this.chunkManager.Factory.Release(chunk.Controller);

            this.generationQueue.CancelChunkGeneration(coordinate);
        }
    }

    /// <summary>
    /// Request a chunk be generated based on a <see cref="ChunkController"/> data.
    /// </summary>
    /// <param name="controller"></param>
    protected void RequestGeneration(Vector3Int coordinates, int lodIndex)
    {
        var task = this.generationQueue.RequestChunkGeneration(coordinates, lodIndex);
        task.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            if (t.Result.MeshData.Vertices.Count == 0)
                return;

            Vector3 worldPos = new Vector3(
                coordinates.x * chunkManager.Configuration.ChunkSize,
                coordinates.y * chunkManager.Configuration.ChunkSize,
                coordinates.z * chunkManager.Configuration.ChunkSize);
            Matrix4x4 transform = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one);

            // Generate mesh and apply color.
            Mesh mesh = this.chunkManager.Generator.GenerateMesh(t.Result, this.chunkManager.Configuration);
            this.chunkManager.Colorizer.UpdateChunkColors(t.Result, transform, this.chunkManager.Configuration);

            mesh.colors = t.Result.MeshData.VerticeColors;

            ChunkRenderData renderData = new ChunkRenderData(coordinates, t.Result, mesh, transform);

            this.SubmitChunk(renderData);

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Submit a chunk to be rendered into the world.
    /// </summary>
    /// <param name="chunkRenderData"></param>
    protected void SubmitChunk(ChunkRenderData chunkRenderData)
    {
        var coord = chunkRenderData.Coordinates;
        int lod = chunkRenderData.LOD;

        if (this.chunkManager.Chunks.TryGetValue(chunkRenderData.Coordinates, out var existing))
        {
            bool ExistingIsGO = existing.RenderType == ChunkRenderType.GameObject;

            // GPU to GO.
            if (!ExistingIsGO && lod == 0)
            {
                var controller = chunkManager.Factory.CreateChunkController(coord, this.cancellationToken.Token);
                chunkRenderData.Controller = controller;
                chunkRenderData.RenderType = ChunkRenderType.GameObject;

                controller.ApplyChunkData(chunkRenderData);
            }
            // GO to GPU
            else if (ExistingIsGO && lod > 0)
            {
                if (existing.Controller != null)
                    chunkManager.Factory.Release(existing.Controller);

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
        else if (lod == 0)
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
        }
        
        chunkManager.Chunks[coord] = chunkRenderData;
    }

    /// <summary>
    /// Update the chunk visibility of each chunk.
    /// </summary>
    private void UpdateVisibility()
    {
        Vector3 camForward = chunkManager.Follower.transform.forward;

        foreach (var chunk in this.chunkManager.Chunks)
        {
            Vector3 size = new Vector3(this.chunkManager.Configuration.ChunkSize, this.chunkManager.Configuration.ChunkSize, this.chunkManager.Configuration.ChunkSize);
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