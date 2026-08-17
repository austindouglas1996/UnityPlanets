namespace MarchingTerrain.Engine.Generation
{
    using MarchingTerrain.Core;
    using MarchingTerrain.Systems.Generation;
    using MarchingTerrain.Systems.Rendering.Unity;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Coordinates the asynchronous-like generation and modification of terrain chunks.
    /// Jobs are queued and processed in small batches during <see cref="Update"/> to avoid frame hitches.
    /// This helps ensure high-priority chunks can be generated first if desired,
    /// though the current implementation uses simple FIFO order.
    /// </summary>
    public class ChunkGenerationProcessor : IDisposable
    {
        private readonly List<ChunkGenerationJob> tmpSurfaceJobs = new(ChunkEngineSettings.SurfaceJobsPerBatch);
        private readonly ChunkGenerationBatcher surfaceBatcher = new();

        /// <summary>
        /// A simple queue to help with items that need to be removed.
        /// </summary>
        private List<(ChunkKey key, int framesLeft)> removalQueue = new();

        /// <summary>
        /// In earlier tests, batching surfaces takes multiple frames so this stops multiple calls. Hmm,
        /// maybe we should have one for generation if it ever takes multiple frames.
        /// </summary>
        private bool SurfaceBusy = false;

        /// <summary>
        /// Manages GPU-side render regions for generated chunks.
        /// </summary>
        private readonly ChunkRenderRouter layerRenderer;

        /// <summary>
        /// Core services used for chunk generation (density generator, colorizer, layout, etc.).
        /// </summary>
        private readonly IChunkServices chunkServices;

        /// <summary>
        /// Creates a new generation processor.
        /// Initializes the GPU render region manager and sets up default material parameters.
        /// </summary>
        public ChunkGenerationProcessor(IChunkServices services, ChunkRenderFeature renderFeature)
        {
            this.chunkServices = services;

            this.layerRenderer = new ChunkRenderRouter(services, services.Generator, ChunkEngineSettings.GenerationJobsPerBatch);

            // Router is shared statically across every ChunkRenderFeature instance, so
            // terrain draws on any renderer that includes the feature — not only the one
            // currently marked Default.
            ChunkRenderFeature.Router = layerRenderer;

            if (renderFeature == null)
            {
                Debug.LogWarning("No ChunkRenderFeature was found on the assigned renderer. " +
                    "Add one to the renderer your camera renders through (the Default renderer if " +
                    "your camera's Renderer is set to Default), or terrain will not appear.");
            }
        }

        /// <summary>
        /// Queues a chunk to be checked for surface data before full generation.
        /// The provided callback will be invoked once the check completes.
        /// </summary>
        public void RequestSurfaceCheck(ChunkKey key, Action<ChunkKey, int, bool> onDone, int parentIndex = -1) =>
            surfaceBatcher.Add(new ChunkGenerationJob(key,onDone,parentIndex));

        /// <summary>
        /// Queues a chunk for full generation.
        /// The provided callback will be invoked once generation is complete.
        /// </summary>
        public void RequestChunkGeneration(ChunkKey key, Action<ChunkKey,int, bool> onDone)
        {
            if (key == ChunkKey.Invalid)
            {
                throw new System.ArgumentException("Invalid key was provided.");
            }

            layerRenderer.Add(new ChunkGenerationJob(key, onDone));
        }

        /// <summary>
        /// Request a chunks collection to be set to update as if a new chunk has been added.
        /// </summary>
        /// <param name="key"></param>
        public void RequestEdit(ChunkKey key)
        {
            layerRenderer.Edit(key);
        }

        /// <summary>
        /// Removes all queued and active references to a given chunk.
        /// Call this when unloading or discarding a chunk to avoid processing it unnecessarily.
        /// </summary>
        public bool RemoveChunk(ChunkKey key, bool now = false)
        {
            bool l = layerRenderer.Remove(key);
            bool s = surfaceBatcher.Remove(key);

            if (l || s)
                return true;

            return false;
        }

        /// <summary>
        /// Returns whether a given key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Exists(ChunkKey key)
        {
            return layerRenderer.Exists(key); 
        }

        /// <summary>
        /// Remove all chunks in the system.
        /// </summary>
        public void RemoveAll()
        {
            this.removalQueue.Clear();
            this.layerRenderer.Clear();
            this.surfaceBatcher.Clear();
        }

        /// <summary>
        /// Mark all collections as dirty.
        /// </summary>
        public void Refresh()
        {
            this.layerRenderer.MarkAsDirty(true);
        }

        /// <summary>
        /// Processes queued surface and generation jobs in small batches,
        /// and updates the GPU render regions.
        /// Call this once per frame.
        /// </summary>
        public void Update()
        {
            this.layerRenderer.Update();

            UpdateSurface();
        }

        /// <summary>
        /// Releases any GPU resources held by the render region manager.
        /// </summary>
        public void Dispose()
        {
            // Clear the shared router so a disposed instance isn't referenced across play
            // sessions (relevant when Enter Play Mode domain reload is disabled).
            if (ChunkRenderFeature.Router == layerRenderer)
                ChunkRenderFeature.Router = null;

            layerRenderer.Dispose();
        }

        /// <summary>
        /// Processes a batch of surface-check jobs.
        /// </summary>
        private void UpdateSurface()
        {
            if (!surfaceBatcher.HasPending) return;

            if (SurfaceBusy)
                return;
            SurfaceBusy = true;

            int n = surfaceBatcher.TryBatch(ChunkEngineSettings.SurfaceJobsPerBatch, tmpSurfaceJobs);
            if (n == 0)
            {
                // Nothing was batched, so no dispatch will fire and nothing will ever
                // clear the busy flag. Release it here or surface checks stall forever
                // and the world silently stops loading.
                SurfaceBusy = false;
                return;
            }

            chunkServices.Generator.DispatchSurfaceCheck(tmpSurfaceJobs, (uint[] surfaceResults) =>
            {
                for (int i = 0; i < n; i++)
                {
                    bool hasSurface = surfaceResults[i] == 1;
                    tmpSurfaceJobs[i].OnDone(tmpSurfaceJobs[i].Key, tmpSurfaceJobs[i].ParentIndex, hasSurface);
                }

                SurfaceBusy = false;
            });
        }
    }
}