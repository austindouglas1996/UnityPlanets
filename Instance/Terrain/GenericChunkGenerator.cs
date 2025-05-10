using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Handles generating chunk meshes and density maps for marching cubes.
/// </summary>
public abstract class GenericChunkGenerator : IChunkGenerator
{
    /// <summary>
    /// Builds a Mesh from the given chunk data.
    /// </summary>
    /// <param name="chunk">The chunk to build the mesh from.</param>
    /// <param name="config">The chunk config used for generation.</param>
    /// <returns>A generated Mesh.</returns>
    public virtual Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        return CreateMapGenerator(config).GenerateMesh(chunk.DensityMap, chunk.MeshData);
    }

    /// <summary>
    /// Generates a new chunk from coordinates using the provided configuration.
    /// </summary>
    /// <param name="coordinates">The chunk coordinates in the world.</param>
    /// <param name="config">The chunk configuration.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>The generated chunk data.</returns>
    public virtual Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, int lodIndex, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var gen = CreateMapGenerator(config);
            var map = gen.Generate(config.ChunkSize, coordinates, lodIndex);

            foreach (var modifier in config.Modifiers)
            {
                if (modifier is IModifyDensity densityMod)
                    densityMod.ModifyDensity(ref map.DensityMap, coordinates, config.MapOptions);

                if (modifier is IModifyFoliageMask foliageMod)
                    foliageMod.ModifyFoliageMask(ref map.FoliageMask, coordinates);
            }

            MeshData data = gen.GenerateMeshData(map.DensityMap, Vector3.zero, map.LODIndex);

            return new ChunkData(map.DensityMap, map.SurfaceMap, map.FoliageMask, data);
        }, token);
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
    public virtual Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            CreateMapGenerator(config).ModifyMapWithBrush(brush, ref data.DensityMap, chunkPos, addingOrSubtracting);
        }, token);
    }

    /// <summary>
    /// Recalculates the mesh data for the given chunk.
    /// </summary>
    /// <param name="data">The chunk data to update.</param>
    /// <param name="config">The chunk config.</param>
    /// <param name="token">Optional cancellation token.</param>
    public virtual Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            data.MeshData = CreateMapGenerator(config).GenerateMeshData(data.DensityMap, Vector3.zero, data.MeshData.LODIndex);
        }, token);
    }

    /// <summary>
    /// Creates a new instance of the map generator based on the config.
    /// </summary>
    /// <param name="config">The chunk configuration.</param>
    /// <returns>A new BaseMarchingCubeGenerator instance.</returns>
    public abstract BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config);
}