using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class GenericChunkGenerator : IChunkGenerator
{
    public virtual Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config)
    {
        return CreateMapGenerator(config).GenerateMesh(chunk.MeshData);
    }

    public virtual Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var gen = CreateMapGenerator(config);
            float[,,] map = gen.Generate(config.ChunkSize, coordinates);
            MeshData data = gen.GenerateMeshData(map, Vector3.zero);

            return new ChunkData(map, data);
        }, token);
    }

    public virtual Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            CreateMapGenerator(config).ModifyMapWithBrush(brush, ref data.DensityMap, chunkPos, brush.WorldHitPoint, addingOrSubtracting);
        }, token);
    }

    public virtual Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default)
    {
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            data.MeshData = CreateMapGenerator(config).GenerateMeshData(data.DensityMap, Vector3.zero);
        }, token);
    }

    protected abstract BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config);
}