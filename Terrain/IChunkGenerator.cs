using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IChunkGenerator
{
    Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config, CancellationToken token = default);
    Task ModifyChunkData(ChunkData data, IChunkConfiguration config, TerrainBrush brush, Vector3Int chunkPos, bool addingOrSubtracting, CancellationToken token = default);
    Task UpdateChunkData(ChunkData data, IChunkConfiguration config, CancellationToken token = default);
    Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config);
}