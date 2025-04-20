using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BiomeMap
{
    private List<IBiome> biomes = new List<IBiome>();

    private int ChunkSize = 32;
    private Vector2Int DefaultSectorSize = new Vector2Int(4, 4);

    private Vector2Int MapSize = new Vector2Int(16, 16);
    private Dictionary<Vector3Int, IBiome> Sectors = new Dictionary<Vector3Int, IBiome>(0);

    public BiomeMap(List<IBiome> biomes)
    {
        this.biomes = biomes;
        EstablishBound();
        PrintBiomeDistribution();
    }

    public void PrintBiomeDistribution()
    {
        Dictionary<string, int> biomeCounts = new();
        int totalSectors = Sectors.Count;

        foreach (var biome in Sectors.Values)
        {
            string name = biome.Name;
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
        IBiome biome = Sectors[coordinate];
        return biome;
    }


    public float Evaluate(float baseVal, Vector3 worldPos)
    {
        float regionSize = 32f;
        float fade = 8f;

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
        float v00 = b00.Evaulate(baseVal, worldPos);
        float v10 = b10.Evaulate(baseVal, worldPos);
        float v01 = b01.Evaulate(baseVal, worldPos);
        float v11 = b11.Evaulate(baseVal, worldPos);

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

    private void EstablishBound()
    {
        Vector2Int mapSize = new Vector2Int(24, 24); // total map size
        Vector2Int groupSize = new Vector2Int(4, 4); // size of each group of sectors
        List<Vector2Int> groupOrigins = new();
        System.Random rand = new();

        int xStart = -mapSize.x;
        int xEnd = mapSize.x;
        int zStart = -mapSize.y;
        int zEnd = mapSize.y;

        for (int gx = xStart; gx < xEnd; gx += groupSize.x)
        {
            for (int gz = zStart; gz < zEnd; gz += groupSize.y)
            {
                groupOrigins.Add(new Vector2Int(gx, gz));
            }
        }


        // 2. Shuffle group origins
        groupOrigins = groupOrigins.OrderBy(_ => rand.Next()).ToList();

        // 3. Assign biomes to each group
        foreach (var origin in groupOrigins)
        {
            IBiome selectedBiome = null;
            int groupArea = groupSize.x * groupSize.y;

            // Loop until a suitable biome is found
            int attempts = 0;
            const int maxAttempts = 10;
            while (attempts++ < maxAttempts)
            {
                var tryBiome = biomes.Random();

                // If mountain, ensure it meets minimum size
                if (tryBiome is MountainBiomeNoise && groupArea < 6)
                    continue;

                selectedBiome = tryBiome;
                break;
            }

            if (selectedBiome == null)
            {
                Debug.LogWarning($"No suitable biome found for group at {origin} after {maxAttempts} tries.");
                selectedBiome = biomes[0]; // fallback (ensure coverage)
            }

            for (int dx = 0; dx < groupSize.x; dx++)
            {
                for (int dz = 0; dz < groupSize.y; dz++)
                {
                    Vector2Int local = new Vector2Int(origin.x + dx, origin.y + dz);
                    if (local.x < xStart || local.x >= xEnd || local.y < zStart || local.y >= zEnd)
                        continue;


                    Vector3Int sectorCoord = new Vector3Int(local.x, 0, local.y);
                    Sectors[sectorCoord] = selectedBiome;
                }
            }
        }

    }
}