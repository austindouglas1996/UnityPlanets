using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared biome library. Holds all biome definitions in one asset,
/// so multiple terrain generators can reuse it.
/// </summary>
[CreateAssetMenu(menuName = "Terrain/Biome Library", fileName = "BiomeLibrary")]
public class BiomeLibrary : ScriptableObject
{
    [SerializeField] private List<Biome> biomes = new();
    public IReadOnlyList<Biome> Biomes => biomes;
}
