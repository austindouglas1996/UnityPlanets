using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

/// <summary>
/// Handles the asynchronous generation and modification of terrain chunks.
/// Uses a priority queue to ensure high-importance chunks (near the player) are processed first.
/// Spawns multiple worker threads that continuously process jobs in the background.
/// </summary>
public class ChunkGenerationProcessor
{
    /// <summary>
    /// A priority queue holding pending generation jobs.
    /// LOD0 jobs are prioritized higher for near-player chunks.
    /// </summary>
    private ChunkGenerationBatcher surfaceBatch = new ChunkGenerationBatcher();
    private ChunkGenerationBatcher generationBatch = new ChunkGenerationBatcher();

    /// <summary>
    /// A system to help with controlling render regions with the GPU. We are unable
    /// to modify buffers.
    /// </summary>
    private ChunkRenderRegionManager regionManager;

    /// <summary>
    /// Central services used during chunk generation (generator, colorizer, layout, etc.).
    /// </summary>
    private IChunkServices chunkServices;

    /// <summary>
    /// Construct a new generation processor, spin up a few worker threads.
    /// </summary>
    public ChunkGenerationProcessor(IChunkServices services)
    {
        this.chunkServices = services;

        Material mat = new Material(Shader.Find("Custom/URP_CustomLitGPU"));
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_UseVertexColor", 1f);

        this.regionManager = new ChunkRenderRegionManager(chunkServices.Generator, mat);
    }

    /// <summary>
    /// Request a new chunk to be checked for surface data before generation.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public void RequestSurfaceCheck(ChunkKey key, Action<bool> onDone)
    {
        var job = new ChunkGenerationJob(key, onDone);
        this.surfaceBatch.Add(job);
    }

    /// <summary>
    /// Request a new chunk to be generated asynchronously.
    /// Prioritizes LOD0 chunks by proximity to the player.
    /// </summary>
    public void RequestChunkGeneration(ChunkKey key, Action<bool> onDone)
    {
        var job = new ChunkGenerationJob(key, onDone);
        this.generationBatch.Add(job);
    }

    public void RemoveChunk(ChunkKey key)
    {
        this.surfaceBatch.Remove(key);
        this.generationBatch.Remove(key);
        this.regionManager.Remove(key);

        this.total--;
    }

    public void Dipose()
    {
        this.regionManager.Dispose();
    }

    public void Draw()
    {
        this.regionManager.Draw();
    }

    /// <summary>
    /// Update the processor and start generating chunks if there is active jobs.
    /// </summary>
    private int surfaceUpdateFrameCounter = 0;

    public void Update()
    {
        regionManager.Update();

        this.UpdateSurface();
        this.UpdateGeneration();
    }


    private int cancelled = 0;
    private int total = 0;

    private void UpdateSurface()
    {
        if (!this.surfaceBatch.HasPending)
            return;

        var batch = this.surfaceBatch.TryBatch(1028);
        var batch1 = new List<ChunkKey>();

        batch.ForEach(r => batch1.Add(r.Key));

        var surfaceChunks = this.chunkServices.Generator.DispatchSurface(batch1);

        int index = 0;

        foreach (var ctx in batch) // Avoid modifying while iterating
        {
            if (surfaceChunks[index] == 0)
            {
                ctx.OnDone(false);
                cancelled++;
            }
            else
            {
                ctx.OnDone(true);
            }

            total++;
            index++;
        }

        Debug.Log($"Cancelled: {cancelled} / out of {total}");
    }

    private void UpdateGeneration()
    {
        if (!this.generationBatch.HasPending || this.generationBatch.Count < 100)
            return;

        var batch = this.generationBatch.TryBatch(128);

        try
        {
            foreach (var job in batch)
            {
                regionManager.Add(job.Key);
                job.OnDone(true);
            }
        }
        catch (Exception ex)
        {
            foreach (var job in batch)
                job.OnDone(false);
            Debug.LogError(ex);
        }
    }
}
