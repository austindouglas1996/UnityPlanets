using System;
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

    protected abstract BaseMarchingCubeGenerator Generator { get; }

    /// <summary>
    /// Generates a new chunk from coordinates using the provided configuration.
    /// </summary>
    /// <param name="coordinates">The chunk coordinates in the world.</param>
    /// <param name="config">The chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>The generated chunk data.</returns>
    public virtual ChunkData GenerateNewChunk(ChunkContext context, CancellationToken token = default)
    {
        var map = Generator.Generate(context);

        foreach (var modifier in configuration.Modifiers)
        {
            //if (modifier is IModifyDensity densityMod)
                //densityMod.ModifyDensity(ref map.DensityMap, coordinates, config.MapOptions);

            //if (modifier is IModifyFoliageMask foliageMod)
                //foliageMod.ModifyFoliageMask(ref map.FoliageMask, coordinates);
        }

        MeshData data = Generator.GenerateMeshData(map, Vector3.zero, context.LODIndex);
        return new ChunkData(map, data, context);
    }

    /// <summary>
    /// Applies a terrain brush to the chunk, modifying its density map.
    /// </summary>
    /// <param name="data">The chunk data to modify.</param>
    /// <param name="config">The chunk config.</param>
    /// <param name="brush">The brush to apply.</param>
    /// <param name="chunkPos">The chunk position in the world.</param>
    /// <param name="addingOrSubtracting">True if adding, false if subtracting.</param>
    /// <param name="token">Optional cancellation token.</param>
    public virtual void ApplyTerrainBrush(ChunkData data, TerrainBrush brush, ChunkContext context, bool addingOrSubtracting, CancellationToken token = default)
    {
        Generator.ModifyMapWithBrush(brush, ref data.DensityMap, context.Coordinates, addingOrSubtracting);
    }

    /// <summary>
    /// Recalculates the mesh data for the given chunk.
    /// </summary>
    /// <param name="data">The chunk data to update.</param>
    /// <param name="config">The chunk config.</param>
    /// <param name="token">Optional cancellation token.</param>
    public virtual void RegenerateMeshData(ChunkData data, CancellationToken token = default)
    {
        data.MeshData = Generator.GenerateMeshData(data.DensityMap, Vector3.zero, data.Context.LODIndex);
    }
}