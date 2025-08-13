using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Handles generating chunk meshes and density maps for marching cubes.
/// </summary>
public abstract class BaseChunkGenerator : IChunkGenerator
{
    private IChunkConfiguration configuration;
    public BaseChunkGenerator(IChunkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    protected IChunkConfiguration Configuration
    {
        get { return configuration; }
    }

    public abstract MarchingCubesGPUDispatcher Generator { get; }

    /// <summary>
    /// Generates a new chunk from coordinates using the provided configuration.
    /// </summary>
    /// <param name="coordinates">The chunk coordinates in the world.</param>
    /// <param name="config">The chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>The generated chunk data.</returns>
    public virtual GPUSet DispatchGeneration(List<ChunkKey> chunkContexts)
    {
        return this.Generator.DispatchGeneration(chunkContexts);
    }

    public void Dispose()
    {
        this.Generator.Dispose();
    }

    public uint[] DispatchSurface(List<ChunkKey> contexts)
    {
        return this.Generator.GetSurfaceMask(contexts);
    }
}