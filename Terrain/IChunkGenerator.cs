using System.Threading.Tasks;
using UnityEngine;

public interface IChunkGenerator
{
    Task<ChunkData> GenerateNewChunk(Vector3Int coordinates, IChunkConfiguration config);
    Task UpdateChunkData(ChunkData data, IChunkConfiguration config);
    Mesh GenerateMesh(ChunkData chunk, IChunkConfiguration config);
}