using UnityEngine;
using System.Collections.Generic;

public interface IChunkLayout
{
    List<Vector3Int> GetActiveChunkCoordinates(Vector3 followerPosition);
    int GetRenderDetail(Vector3Int followerCoordinates, Vector3Int chunkCoordinate);
}