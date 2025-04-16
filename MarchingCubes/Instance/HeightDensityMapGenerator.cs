using System;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : BaseMarchingCubeGenerator
{
    public HeightDensityMapGenerator(DensityMapOptions options) : base(options)
    {
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

                    float sampleFreq = Options.Frequency * Options.NoiseScale;

                    // 3D noise for caves and structure
                    float noise = Perlin.Fbm(
                        (worldX + Options.Seed) * sampleFreq,
                        (worldY + Options.Seed) * sampleFreq,
                        (worldZ + Options.Seed) * sampleFreq,
                        Options.Octaves
                    ) * Options.Amplitude;

                    float value = (25 - worldY) + (noise * Options.NoiseMultiplier);
                    float valueNormalized = value * 0.5f;

                    // Scale to match Marching Cubes range
                    densityMap[x, y, z] = valueNormalized;

                    // detect the surface if we haven't yet and this crosses ISO level
                    if (surfaceMap[x, z] < 0f && valueNormalized > Options.ISOLevel)
                    {
                        surfaceMap[x, z] = y;
                    }
                }
            }
        }

        return new Tuple<float[,,], float[,]>(densityMap, surfaceMap);
    }

    public override MeshData GenerateMeshData(float[,,] densityMap, Vector3 chunkOffset)
    {
        MeshData initialData = base.GenerateMeshData(densityMap, chunkOffset);
        Vector2[] uvs = new Vector2[initialData.Vertices.Count];

        for (int i = 0; i < initialData.Vertices.Count; i++)
        {
            Vector3 v = initialData.Vertices[i].normalized;
            uvs[i] = new Vector2(v.x,v.y);
        }

        // Set the UV with our modified data.
        initialData.UVs = uvs.ToList();

        return initialData;
    }
}