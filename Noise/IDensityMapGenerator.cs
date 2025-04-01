using UnityEngine;

public interface IDensityMapGenerator
{
    DensityMapOptions Options { get; }
    float[,,] Generate(Vector3Int size, Vector3Int chunkCoordinates);
}