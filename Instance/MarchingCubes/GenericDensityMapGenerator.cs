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

        if (!ShouldGenerateChunk(chunkCoordinates, chunkSize))
        {
            return CreateEmptyChunk(size.x);
        }

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

    private System.Random random = new System.Random();
    private bool ShouldGenerateChunk(Vector3Int chunkCoords, int chunkSize)
    {
        int sampleCount = 256; // number of points to test

        for (int i = 0; i < sampleCount; i++)
        {
            // Random sample within chunk
            int x = random.Next(0, chunkSize);
            int y = random.Next(0, chunkSize);
            int z = random.Next(0, chunkSize);

            int worldX = chunkCoords.x * chunkSize + x;
            int worldY = chunkCoords.y * chunkSize + y;
            int worldZ = chunkCoords.z * chunkSize + z;

            float value = GetValueForWorldPosition(worldX, worldY, worldZ);

            if (value > Options.ISOLevel - 96 && value < Options.ISOLevel + 96)
            {
                return true; // something interesting here (surface crossing)
            }
        }

        return false; // skip chunk, it's boring
    }

    private DensityMapData CreateEmptyChunk(int size)
    {
        float[,,] densityMap = new float[size + 1, size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z <= size; z++)
                    densityMap[x, y, z] = 0; // fully empty

        float[,] surfaceMap = new float[size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int z = 0; z <= size; z++)
                surfaceMap[x, z] = -1f;

        float[,,] foliageMask = new float[size + 1, size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z <= size; z++)
                    foliageMask[x, y, z] = 1f; // still allow trees, etc.

        return new DensityMapData(densityMap, surfaceMap, foliageMask);
    }



    protected abstract float GetValueForWorldPosition(float worldX, float worldY, float worldZ);
}