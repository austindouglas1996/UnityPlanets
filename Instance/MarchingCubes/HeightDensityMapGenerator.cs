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
        Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

        // CONTINENT SHAPE: where oceans, landmasses are
        float continentNoise = Perlin.Fbm(
            (worldX + Options.Seed) * Options.ContinentFrequency,
            (worldZ + Options.Seed) * Options.ContinentFrequency,
            3
        );
        continentNoise = Mathf.Clamp01((continentNoise + 1f) * 0.5f);
        continentNoise = Mathf.SmoothStep(0f, 1f, continentNoise);

        // DETAIL NOISE: small hills and bumps
        float detailNoise = Perlin.Fbm(
            (worldX + Options.Seed + 1234) * Options.DetailFrequency,
            (worldZ + Options.Seed + 1234) * Options.DetailFrequency,
            6
        );

        // FLATNESS NOISE: makes terrain flatter where needed
        float flatness = Perlin.Fbm(
            (worldX + Options.Seed + 5555) * Options.FlatnessFrequency,
            (worldZ + Options.Seed + 5555) * Options.FlatnessFrequency,
            4
        );
        flatness = Mathf.Clamp01((flatness + 1f) * 0.5f);
        flatness = Mathf.Pow(1f - flatness, Options.FlatnessStrength); 

        // MOUNTAIN MASK: Where mountains *can* appear
        float mountainMask = Perlin.Fbm(
            (worldX + Options.Seed + 9999) * 0.001f, 
            (worldZ + Options.Seed + 9999) * 0.001f,
            3
        );
        mountainMask = Mathf.Clamp01((mountainMask + 1f) * 0.5f);  // normalize

        // Should we allow mountains here?
        bool allowMountains = mountainMask > 0.9f;

        // Now only build mountain noise if allowed
        float mountainNoise = 0f;
        if (allowMountains)
        {
            mountainNoise = Perlin.Fbm(
                (worldX + Options.Seed + 999) * Options.MountainFrequency,
                (worldZ + Options.Seed + 999) * Options.MountainFrequency,
                5
            );
            mountainNoise = Mathf.Pow(mountainNoise, Options.MountainSharpness);  // sharper peaks
        }

        // Calculate the terrain height at this (X,Z)
        float rawTerrainHeight =
            continentNoise * Options.ContinentAmplitude +           // base continents
            mountainNoise * Options.MountainAmplitude +             // mountains (only in mask zones)
            detailNoise * Options.DetailAmplitude * flatness;       // detail bumps scaled by flatness

        float normalizedHeight = Mathf.InverseLerp(0f, Options.ContinentAmplitude + Options.MountainAmplitude, rawTerrainHeight);
        float remappedHeight = Options.TerrainShapeCurve.Evaluate(normalizedHeight);

        float finalHeight = remappedHeight * Options.TotalHeightScale;

        float value = -(worldY - finalHeight);

        return value;
    }


    public override MeshData GenerateMeshData(float[,,] densityMap, Vector3 chunkOffset, int lodIndex = 5)
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