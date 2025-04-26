using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BiomeMap
{
    private List<IBiome> biomes = new List<IBiome>();
    private Dictionary<int, IBiome> biomesById = new Dictionary<int, IBiome>();

    private int ChunkSize = 32;

    private Dictionary<Vector2Int, BiomeSector> Sectors = new Dictionary<Vector2Int, BiomeSector>(0);

    private BiomeMapper mapper;

    public BiomeMap(List<IBiome> biomes)
    {
        this.biomes = biomes;
        this.biomesById = biomes.ToDictionary(b => b.Id);

        this.mapper = new BiomeMapper(this.biomes);
        this.Sectors = this.mapper.CreateMap(new Vector2Int(24, 24), false);

        PrintBiomeDistribution();
    }

    public void PrintBiomeDistribution()
    {
        Dictionary<string, int> biomeCounts = new();
        int totalSectors = Sectors.Count;

        foreach (var biome in Sectors.Values)
        {
            string name = biomesById[biome.BiomeId].Name;
            if (!biomeCounts.ContainsKey(name))
                biomeCounts[name] = 0;

            biomeCounts[name]++;
        }

        foreach (var kvp in biomeCounts.OrderByDescending(kvp => kvp.Value))
        {
            float percent = (kvp.Value / (float)totalSectors) * 100f;
            Debug.Log($"{kvp.Key}: {kvp.Value} sectors ({percent:F2}%)");
        }
    }


    public IBiome GetBiome(Vector3Int coordinate)
    {
        Vector2Int v = new Vector2Int(coordinate.x, coordinate.z);
        IBiome biome = biomesById[Sectors[v].BiomeId];
        return biome;
    }


    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        float regionSize = 32f;

        // Get base sector coordinates
        int baseX = Mathf.FloorToInt(worldPos.x / regionSize);
        int baseZ = Mathf.FloorToInt(worldPos.z / regionSize);

        Vector3Int c00 = new Vector3Int(baseX, 0, baseZ);
        Vector3Int c10 = c00 + Vector3Int.right;
        Vector3Int c01 = c00 + Vector3Int.forward;
        Vector3Int c11 = c00 + Vector3Int.right + Vector3Int.forward;

        IBiome b00 = GetBiome(c00);
        IBiome b10 = GetBiome(c10);
        IBiome b01 = GetBiome(c01);
        IBiome b11 = GetBiome(c11);

        // Local position inside region
        float localX = Mathf.InverseLerp(0f, regionSize, worldPos.x - baseX * regionSize);
        float localZ = Mathf.InverseLerp(0f, regionSize, worldPos.z - baseZ * regionSize);

        // Optionally add fade control (soften near edge only)
        float fx = Mathf.SmoothStep(0f, 1f, localX);
        float fz = Mathf.SmoothStep(0f, 1f, localZ);

        // Sample biome values
        float v00 = b00.Evaluate(baseVal, worldPos);
        float v10 = b10.Evaluate(baseVal, worldPos);
        float v01 = b01.Evaluate(baseVal, worldPos);
        float v11 = b11.Evaluate(baseVal, worldPos);

        // Bilinear interpolation between 4 values
        float i0 = Mathf.Lerp(v00, v10, fx);
        float i1 = Mathf.Lerp(v01, v11, fx);
        return Mathf.Lerp(i0, i1, fz);
    }

    public Color EvaluateColor(Vector3 worldPos)
    {
        float regionSize = 32f;

        int baseX = Mathf.FloorToInt(worldPos.x / regionSize);
        int baseZ = Mathf.FloorToInt(worldPos.z / regionSize);

        Vector3Int c00 = new Vector3Int(baseX, 0, baseZ);
        Vector3Int c10 = c00 + Vector3Int.right;
        Vector3Int c01 = c00 + Vector3Int.forward;
        Vector3Int c11 = c00 + Vector3Int.right + Vector3Int.forward;

        IBiome b00 = GetBiome(c00);
        IBiome b10 = GetBiome(c10);
        IBiome b01 = GetBiome(c01);
        IBiome b11 = GetBiome(c11);

        float localX = Mathf.InverseLerp(0f, regionSize, worldPos.x - baseX * regionSize);
        float localZ = Mathf.InverseLerp(0f, regionSize, worldPos.z - baseZ * regionSize);

        float fx = Mathf.SmoothStep(0f, 1f, localX);
        float fz = Mathf.SmoothStep(0f, 1f, localZ);

        Color c0 = Color.Lerp(b00.DensityMapOptions.SurfaceColorRange.Evaluate(worldPos.y), b10.DensityMapOptions.SurfaceColorRange.Evaluate(worldPos.y), fx);
        Color c1 = Color.Lerp(b01.DensityMapOptions.SurfaceColorRange.Evaluate(worldPos.y), b11.DensityMapOptions.SurfaceColorRange.Evaluate(worldPos.y), fx);
        return Color.Lerp(c0, c1, fz);
    }
}