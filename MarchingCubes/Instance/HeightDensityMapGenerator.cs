using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    public HeightDensityMapGenerator(DensityMapOptions options) : base(options)
    {
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

        float value = (25 - worldY) + (noise * Options.NoiseMultiplier);

        // Scale to match Marching Cubes range
        return value * 0.5f;
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