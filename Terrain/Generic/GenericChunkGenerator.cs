using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Handles generating chunk meshes and density maps for marching cubes.
/// </summary>
public abstract class GenericChunkGenerator : IChunkGenerator
{
    private IChunkConfiguration configuration;
    public GenericChunkGenerator(IChunkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    protected IChunkConfiguration Configuration
    {
        get { return configuration; }
    }

    public abstract BaseMarchingCubeGenerator Generator { get; }

    /// <summary>
    /// Generates a new chunk from coordinates using the provided configuration.
    /// </summary>
    /// <param name="coordinates">The chunk coordinates in the world.</param>
    /// <param name="config">The chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>The generated chunk data.</returns>
    public virtual void DispatchGeneration(List<ChunkContext> chunkContexts, CancellationToken token)
    {
        this.Generator.DispatchGeneration(chunkContexts);
    }

    public void Dispose()
    {
        this.Generator.Dispose();
    }

    public void Draw()
    {
        this.Generator.Draw();
    }

    public void DrawGizmo()
    {
        this.Generator.DrawGizmo();
    }
}