using NUnit.Framework;
using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override DensityMapData Generate(int chunkSize, Vector3Int chunkCoordinates)
    {
        Vector3Int size = new Vector3Int(chunkSize, chunkSize, chunkSize);

        // Create a density map with an extra layer of padding for marching cubes
        float[,,] densityMap = new float[size.x + 1, size.y + 1, size.z + 1];

        // Create a surface map. Initially set it to -1.
        float[,] surfaceMap = new float[size.x + 1, size.z + 1];
        for (int x = 0; x < size.x + 1; x++)
            for (int z = 0; z < size.z + 1; z++)
                surfaceMap[x, z] = -1f; // initialize to invalid

        float[,,] foliageMask = new float[size.x + 1, size.y + 1, size.z + 1];
        for (int x = 0; x < size.x + 1; x++)
            for (int y = 0; y < size.y + 1; y++)
                for (int z = 0; z < size.z + 1; z++)
                    foliageMask[x, y, z] = 1f;

        for (int x = 0; x < size.x + 1; x++)
        {
            for (int y = 0; y < size.y + 1; y++)
            {
                for (int z = 0; z < size.z + 1; z++)
                {
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    float val = GetValueForWorldPosition(worldX, worldY, worldZ);
                    densityMap[x, y, z] = val;

                    if (surfaceMap[x, z] < 0f && val > Options.ISOLevel)
                        surfaceMap[x, z] = y - 1;
                }
            }
        }

        return new DensityMapData(densityMap, surfaceMap, foliageMask);
    }

    protected abstract float GetValueForWorldPosition(float worldX, float worldY, float worldZ);
}