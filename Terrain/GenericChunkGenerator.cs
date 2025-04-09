using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class GenericChunkGenerator<TMapGenerator> : IChunkGenerator
    where TMapGenerator : BaseMarchingCubeGenerator
{
    public Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        return CreateInstance(config).GenerateMesh(chunk.MeshData);
    }

    public Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var gen = CreateInstance(config);
            float[,,] map = gen.Generate(config.ChunkSize, coordinates);
            MeshData data = gen.GenerateMeshData(map, Vector3.zero);

            return new ChunkData(map, data);
        }, token);
    }

    public Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            CreateInstance(config).ModifyMapWithBrush(brush, ref data.DensityMap, chunkPos, brush.WorldHitPoint, addingOrSubtracting);
        }, token);
    }

    public Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            data.MeshData = CreateInstance(config).GenerateMeshData(data.DensityMap, Vector3.zero);
        }, token);
    }

    private TMapGenerator CreateInstance(IChunkConfiguration config)
    {
        return (TMapGenerator)Activator.CreateInstance(typeof(TMapGenerator), config);
    }
}