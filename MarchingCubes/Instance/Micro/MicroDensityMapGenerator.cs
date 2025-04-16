using System;
using UnityEngine;

public abstract class MicroDensityMapGenerator : BaseMarchingCubeGenerator
{
    private ChunkController Controller;

    public MicroDensityMapGenerator(ChunkController owner, DensityMapOptions options) : base(options)
    {
        this.Controller = owner;
    }

    public override DensityMapOptions Options { get; set; }

    public override Tuple<float[,,], float[,]> Generate(int chunkSize, Vector3Int chunkCoordinates)
    {
        Vector3Int size = new Vector3Int(chunkSize, chunkSize, chunkSize);

        // Create a density map with an extra layer of padding for marching cubes
        float[,,] densityMap = new float[size.x + 1, size.y + 1, size.z + 1];

        // Create a surface map. Initially set it to -1.
        float[,] surfaceMap = new float[size.x + 1, size.z + 1];
        for (int x = 0; x < size.x + 1; x++)
            for (int z = 0; z < size.z + 1; z++)
                surfaceMap[x, z] = -1f; // initialize to invalid

        for (int x = 0; x < size.x + 1; x++)
        {
            for (int y = 0; y < size.y + 1; y++)
            {
                for (int z = 0; z < size.z + 1; z++)
                {
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    float value = GetMapValue(new Vector3(worldX, worldY, worldZ));
                    densityMap[x, y, z] = value;

                    // detect the surface if we haven't yet and this crosses ISO level
                    if (surfaceMap[x, z] < 0f && value > Options.ISOLevel)
                    {
                        surfaceMap[x, z] = y;
                    }
                }
            }
        }

        return new Tuple<float[,,], float[,]>(densityMap, surfaceMap);
    }

    protected abstract float GetMapValue(Vector3 worldPos);
}