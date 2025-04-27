using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BiomeSector
{
    public BiomeSector(int biomeId)
    {
        this.BiomeId = biomeId;
    }

    public int BiomeId;
    public float BlendFactor;
}

public class BiomeTiler
{
    private Vector2Int[] dirs = new[]
    {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

    private List<IBiome> biomes = new List<IBiome>();

    private int Seed = 3271;
    private System.Random rnd;

    public BiomeTiler(List<IBiome> biomes)
    {
        this.biomes = biomes;
        this.rnd = new System.Random(Seed);
    }

    public Dictionary<Vector2Int, BiomeSector> CreateMap(Vector2Int initialSize, bool expand = true)
    {
        Dictionary<Vector2Int, BiomeSector> sectors = new Dictionary<Vector2Int, BiomeSector>();

        // Fill with blank data.
        InitialFill(ref sectors, initialSize);

        // Fill with actual data.
        Fill(ref sectors);

        for (int i = 0; i < 4; i++)
        {
            // Clean up random sectors.
            Clean(sectors);
        }

        // Should we encase the data?
        if (expand)
            sectors = Expand(sectors);

        // Get sector blending.
        SetBlend(ref sectors);

        return sectors;
    }

    private void InitialFill(ref Dictionary<Vector2Int, BiomeSector> coll, Vector2Int size)
    {
        for (int x = -size.x; x < size.x; x++)
        {
            for (int z = -size.y; z < size.y; z++)
            {
                coll.Add(new Vector2Int(x, z), null);
            }
        }
    }

    private void Fill(ref Dictionary<Vector2Int, BiomeSector> coll)
    {
        // Pick a random start cell & give it a random biome
        var allKeys = coll.Keys.ToList();
        var start = allKeys[rnd.Next(allKeys.Count)];

        coll[start] = new BiomeSector(biomes[rnd.Next(biomes.Count)].Id);

        // BFS‑style flood fill from that start
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            foreach (var d in dirs)
            {
                var nxt = new Vector2Int(cur.x + d.x, cur.y + d.y);

                // skip out‑of‑bounds or already filled
                if (!coll.ContainsKey(nxt) || coll[nxt] != null)
                    continue;

                var allowed = GetAllowedBiomesForCell(nxt, coll);

                // pick one & enqueue
                var pickIndex = allowed.ElementAt(rnd.Next(allowed.Count));
                coll[nxt] = new BiomeSector(biomes.First(b => b.Id == pickIndex).Id);
                queue.Enqueue(nxt);
            }
        }
    }

    private void Clean(Dictionary<Vector2Int, BiomeSector> coll)
    {
        var dirs = new[]
        {
                new Vector2Int( 1,  0),
                new Vector2Int(-1,  0),
                new Vector2Int( 0,  1),
                new Vector2Int( 0, -1),
        };

        foreach (var kvp in coll)
        {
            int initalBiome = kvp.Value.BiomeId;
            var allowed = GetAllowedBiomesForCell(kvp.Key, coll);
            Dictionary<int, int> neighborBiomes = new Dictionary<int, int>();

            foreach (var dir in dirs)
            {
                Vector2Int neighborPos = kvp.Key + dir;
                if (coll.TryGetValue(neighborPos, out var neighbor))
                {
                    if (neighborBiomes.ContainsKey(neighbor.BiomeId))
                        neighborBiomes[neighbor.BiomeId]++;
                    else
                        neighborBiomes.Add(neighbor.BiomeId, 1);
                }
            }

            var differentNeighbors = neighborBiomes.Where(kv => kv.Key != initalBiome).ToList();
            bool hasTwoDoubles = differentNeighbors.Count(kv => kv.Value == 2) >= 2;

            if (hasTwoDoubles)
            {
                kvp.Value.BiomeId = allowed.ElementAt(rnd.Next(allowed.Count));
            }
            else
            {
                var strongNeighbor = differentNeighbors.FirstOrDefault(kv => kv.Value >= 3 && allowed.Contains(kv.Key));
                if (!strongNeighbor.Equals(default(KeyValuePair<int, int>)))
                {
                    kvp.Value.BiomeId = strongNeighbor.Key;
                }
            }
        }
    }

    private Dictionary<Vector2Int, BiomeSector> Expand(Dictionary<Vector2Int, BiomeSector> initalCollection)
    {
        Dictionary<Vector2Int, BiomeSector> newColl = new Dictionary<Vector2Int, BiomeSector>();

        foreach (var kvp in initalCollection)
        {
            var original = kvp.Key;
            var biome = kvp.Value;

            // Scale coordinates up by 3 so each original gets its own "space"
            int baseX = original.x * 3;
            int baseY = original.y * 3;

            for (int dx = 0; dx < 3; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    var expandedKey = new Vector2Int(baseX + dx, baseY + dy);
                    newColl[expandedKey] = biome;
                }
            }
        }

        return newColl;
    }

    private void SetBlend(ref Dictionary<Vector2Int, BiomeSector> coll)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
                new Vector2Int( 0,  1),  // Top
                new Vector2Int( 1,  1),  // Top Right
                new Vector2Int( 1,  0),  // Right
                new Vector2Int( 1, -1),  // Bottom Right
                new Vector2Int( 0, -1),  // Bottom
                new Vector2Int(-1, -1),  // Bottom Left
                new Vector2Int(-1,  0),  // Left
                new Vector2Int(-1,  1)   // Top Left
        };

        foreach (var kvp in coll)
        {
            var position = kvp.Key;
            var biome = kvp.Value;

            int differentNeighbors = 0;
            int totalChecked = 0;

            foreach (var dir in directions)
            {
                var neighborPos = new Vector2Int(position.x + dir.x, position.y + dir.y);

                if (coll.TryGetValue(neighborPos, out var neighborBiome))
                {
                    totalChecked++;

                    if (neighborBiome.BiomeId != biome.BiomeId)
                    {
                        differentNeighbors++;
                    }
                }
            }

            if (differentNeighbors == 0)
            {
                biome.BlendFactor = 1f; // Inside
            }
            else
            {
                // For now, linear: more different neighbors = lower blend percent
                biome.BlendFactor = 1f - (differentNeighbors / 8f);
            }
        }
    }

    private HashSet<int> GetAllowedBiomesForCell(Vector2Int nxt, Dictionary<Vector2Int, BiomeSector> coll)
    {
        var votes = new Dictionary<int, int>();
        int maxIndex = biomes.Count - 1;

        bool nearWater = false;

        foreach (var dir in dirs)
        {
            var nb = nxt + dir;
            if (coll.TryGetValue(nb, out var b) && b != null)
            {
                int ni = b.BiomeId;

                if (ni == 0 || ni == 1)
                    nearWater = true;

                var low = System.Math.Max(0, ni - 1);
                var high = System.Math.Min(maxIndex, ni + 1);

                for (int i = low; i <= high; i++)
                {
                    if (!votes.ContainsKey(i))
                        votes[i] = 0;

                    votes[i]++;
                }
            }
        }

        if (!nearWater)
            votes.Remove(2);

        // Pick biomes with the highest vote(s)
        int maxVote = votes.Values.DefaultIfEmpty(0).Max();
        return votes
            .Where(kvp => kvp.Value == maxVote)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }
}
