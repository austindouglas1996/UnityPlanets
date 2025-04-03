using UnityEngine;

public interface IDensityMapGenerator
{
    DensityMapOptions Options { get; }
    float[,,] Generate(int chunkSize, Vector3Int chunkCoordinates);
}