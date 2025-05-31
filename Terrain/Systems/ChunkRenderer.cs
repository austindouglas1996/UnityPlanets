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
    [Tooltip("Should chunks the follower cannot see be automatically hidden?")]
    public bool AutomaticallyHideChunksOutOfView = true;

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
                //UpdateVisibility();
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
        this.chunkServices.ControllerFactory.Release(renderData.Controller);
        //renderData.Controller = null;

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
            var controller = chunkServices.ControllerFactory.CreateChunkController(chunkRenderData.Context, this.cancellationToken.Token);
            chunkRenderData.SetController(controller);
            chunkRenderData.RenderType = ChunkRenderType.GameObject;
            chunkManager.Chunks[chunkRenderData.Context] = chunkRenderData;
            controller.ApplyChunkData(chunkRenderData);

            /*
            var coord = chunkRenderData.Context.Coordinates;
            if (chunkRenderData.LOD == 0)
            {

            }
            else
            {
                chunkRenderData.RenderType = ChunkRenderType.GPU;
                chunkRenderData.Controller = null;
            }

            chunkManager.Chunks[chunkRenderData.Context] = chunkRenderData;
            */
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
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