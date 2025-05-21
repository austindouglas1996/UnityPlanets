using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    public HeightDensityMapGenerator(IChunkColorizer color, DensityMapOptions options) : base(color, options)
    {
    }

    protected override float GetHeightForWorldPosition(float worldX, float worldZ)
    {
        float x = worldX + Options.Seed;
        float z = worldZ + Options.Seed;

        // CONTINENT SHAPE
        float continentNoise = Perlin.Fbm(x * Options.ContinentFrequency, z * Options.ContinentFrequency, 3);
        continentNoise = Mathf.SmoothStep(0f, 1f, (continentNoise + 1f) * 0.5f);

        // DETAIL
        float detailNoise = Perlin.Fbm((x + 1234f) * Options.DetailFrequency, (z + 1234f) * Options.DetailFrequency, 3);

        // FLATNESS
        float flatness = Perlin.Fbm((x + 5555f) * Options.FlatnessFrequency, (z + 5555f) * Options.FlatnessFrequency, 2);
        flatness = Mathf.Clamp01((flatness + 1f) * 0.5f);
        flatness = Mathf.Pow(1f - flatness, Options.FlatnessStrength);

        // MOUNTAIN MASK
        float mountainMask = Perlin.Fbm((x + 9999f) * 0.001f, (z + 9999f) * 0.001f, 2);
        mountainMask = Mathf.Clamp01((mountainMask + 1f) * 0.5f);
        float mountainWeight = Mathf.SmoothStep(0.85f, 1f, mountainMask);

        // MOUNTAIN NOISE
        float mountainNoise = Perlin.Fbm((x + 999f) * Options.MountainFrequency, (z + 999f) * Options.MountainFrequency, 3);
        mountainNoise = Mathf.Pow(mountainNoise, Options.MountainSharpness) * mountainWeight;

        float rawTerrainHeight =
            continentNoise * Options.ContinentAmplitude +
            mountainNoise * Options.MountainAmplitude +
            detailNoise * Options.DetailAmplitude * flatness;

        // Normalize + remap
        float normalizedHeight = rawTerrainHeight / (Options.ContinentAmplitude + Options.MountainAmplitude);
        float remappedHeight = Options.TerrainShapeCurve.Evaluate(normalizedHeight);
        float finalHeight = remappedHeight * Options.TotalHeightScale;

        return finalHeight;
    }

    protected override float GetValueForWorldPosition(float worldX, float worldY, float worldZ)
    {
        return -(worldY - GetHeightForWorldPosition(worldX,worldZ));
    }

    



    public override MeshData GenerateMeshData(DensityMap densityMap, Vector3 chunkOffset, int lodIndex = 5)
    {
        MeshData initialData = base.GenerateMeshData(densityMap, chunkOffset, lodIndex);
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