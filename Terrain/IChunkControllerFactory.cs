using UnityEngine;

public interface IChunkControllerFactory
{
    ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent);
}