using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[RequireComponent (typeof(ChunkManager))]
public class ChunkRenderer : MonoBehaviour
{
    private ChunkManager chunkManager;
    private IChunkServices chunkServices;
    public ChunkGenerationProcessor generationQueue;

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
            if (chunk.State == ChunkRenderState.GPU && chunk.IsActive)
            {
                Graphics.DrawMesh(chunk.Mesh, chunk.LocalToWorld, material, 0);
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

        this.chunkServices = services;
        this.generationQueue = new ChunkGenerationProcessor(services, cancellationToken.Token);

        isInitialized = true;
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

        this.generationQueue.CancelChunkGeneration(renderData.Context.Coordinates, renderData.Context.LODIndex);

        if (renderData.State == ChunkRenderState.GameObject && renderData.Controller != null)
        {
            this.chunkServices.ControllerFactory.Release(renderData.Controller);
            renderData.SetController(null);
        }

        this.chunkManager.Chunks.Remove(renderData.Context);
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
                || t.Result.MeshData.IsEmpty
                || !t.Result.MeshData.IsRenderable)
                {
                    // We still return a value here so the 
                    // tree does not keep waiting for a child that is never
                    // going to come ):
                    quadNode.SetRenderData(context.Coordinates, null);
                    return;
                }

                Matrix4x4 transform = Matrix4x4.TRS(context.WorldPosition, Quaternion.identity, Vector3.one);

                // Generate mesh and apply color.
                ChunkRenderData renderData = new ChunkRenderData(context, t.Result, transform);
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
    /// Submit a chunk to be rendered into the world.
    /// </summary>
    /// <param name="chunkRenderData"></param>
    private void SubmitNewChunk(ChunkRenderData chunkRenderData)
    {
        try
        {
            var coord = chunkRenderData.Context.Coordinates;
            if (chunkRenderData.LOD == 0)
            {
                var controller = chunkServices.ControllerFactory.CreateChunkController(chunkRenderData.Context, this.cancellationToken.Token);
                chunkRenderData.SetController(controller);
                chunkRenderData.State = ChunkRenderState.GameObject;
                chunkManager.Chunks[chunkRenderData.Context] = chunkRenderData;
                controller.ApplyChunkData(chunkRenderData);
            }
            else
            {
                chunkRenderData.State = ChunkRenderState.GPU;
                chunkRenderData.SetController(null);
            }

            chunkManager.Chunks[chunkRenderData.Context] = chunkRenderData;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }
}