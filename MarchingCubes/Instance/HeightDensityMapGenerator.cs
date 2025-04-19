using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    private BiomeNoise biomeNoise;
    public HeightDensityMapGenerator(DensityMapOptions options) : base(options)
    {
        biomeNoise = new BiomeNoise(new IBiome[] { new PlainBiome(0.4f, 0.1f, 52), new OceanBiome(depthScale: 0.4f, depthStrength: 20f, seed: 88f), new MountainBiome(5f, 60f, 52) }, 0.003f, 52);
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

        float value = -worldY + (noise * Options.NoiseMultiplier);
        value = biomeNoise.Evaluate(value, new Vector3(worldX, worldY, worldZ));

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