using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    private BiomeMap biomeMap;

    public HeightDensityMapGenerator(BiomeMap biomeMap, DensityMapOptions options) : base(options)
    {
        this.biomeMap = biomeMap;
    }

    protected override float GetValueForWorldPosition(float worldX, float worldY, float worldZ)
    {
        float sampleFreq = Options.Frequency * Options.NoiseScale;

        // 3D noise for caves and structure
        float noise = Perlin.Fbm(
            (worldX + Options.Seed) * sampleFreq,
            (worldY + Options.Seed) * sampleFreq,
            (worldZ + Options.Seed) * sampleFreq,
            Options.Octaves
        ) * Options.Amplitude;

        Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

        float value = -worldY + (noise * Options.NoiseMultiplier);
        value = biomeMap.Evaluate(value, worldPos);

        // Scale to match Marching Cubes range
        return value;
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