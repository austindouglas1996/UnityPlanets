using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BaseChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private DebugOptions debugOptions;
    public DebugOptions DebugOptions => debugOptions;


    [SerializeField] private TerrainDensityOptions densityMapOptions;
    public TerrainDensityOptions DensityOptions => densityMapOptions;


    [SerializeField] private PlanetDensityOptions planetMapOptions;
    public PlanetDensityOptions PlanetOptions => planetMapOptions;

    [SerializeField]
    private List<Biome> biomes = new()
    {
        // Ocean & Low water
        new Biome("Deep Ocean",    BiomeHeight.BelowWater, BiomeTemperature.Cold,   BiomeHumidity.Wet,    BiomeFoliage.None),
        new Biome("Shallow Ocean", BiomeHeight.BelowWater, BiomeTemperature.Normal, BiomeHumidity.Wet,    BiomeFoliage.Sparse),
        new Biome("Coral Shore",   BiomeHeight.BelowWater, BiomeTemperature.Warm,   BiomeHumidity.Wet,    BiomeFoliage.Dense),

        // Beaches
        new Biome("Beach",         BiomeHeight.WaterLevel, BiomeTemperature.Normal, BiomeHumidity.Normal, BiomeFoliage.None),
        new Biome("Icy Shore",     BiomeHeight.WaterLevel, BiomeTemperature.Cold,   BiomeHumidity.Normal, BiomeFoliage.None),
        new Biome("Tropical Shore",BiomeHeight.WaterLevel, BiomeTemperature.Hot,    BiomeHumidity.Wet,    BiomeFoliage.Sparse),

        // Grasslands
        new Biome("Plains",        BiomeHeight.AboveWater, BiomeTemperature.Normal, BiomeHumidity.Normal, BiomeFoliage.None),
        new Biome("Grasslands",    BiomeHeight.AboveWater, BiomeTemperature.Warm,   BiomeHumidity.Normal, BiomeFoliage.Sparse),
        new Biome("Savanna",       BiomeHeight.AboveWater, BiomeTemperature.Hot,    BiomeHumidity.Normal, BiomeFoliage.Sparse),

        // Forests
        new Biome("Forest",        BiomeHeight.AboveWater, BiomeTemperature.Normal, BiomeHumidity.Normal, BiomeFoliage.Dense),
        new Biome("Taiga",         BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Normal, BiomeFoliage.Dense),
        new Biome("Jungle",        BiomeHeight.AboveWater, BiomeTemperature.Hot,    BiomeHumidity.Wet,    BiomeFoliage.Dense),

        // Swamps / Wetlands
        new Biome("Swamp",         BiomeHeight.AboveWater, BiomeTemperature.Normal, BiomeHumidity.Wet,    BiomeFoliage.Dense),
        new Biome("Marsh",         BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Wet,    BiomeFoliage.Sparse),

        // Mountains / High terrain
        new Biome("Mountains",     BiomeHeight.AboveWater, BiomeTemperature.Warm,   BiomeHumidity.Dry,    BiomeFoliage.None),
        new Biome("Snowy Peaks",   BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Dry,    BiomeFoliage.None),

        // Deserts
        new Biome("Desert",        BiomeHeight.AboveWater, BiomeTemperature.Hot,    BiomeHumidity.Dry,    BiomeFoliage.None),
        new Biome("Ice Desert",    BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Dry,    BiomeFoliage.None),

        // Polar regions
        new Biome("Tundra",        BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Normal, BiomeFoliage.Sparse),
        new Biome("Polar Icecap",  BiomeHeight.AboveWater, BiomeTemperature.Cold,   BiomeHumidity.Wet,    BiomeFoliage.None)
    };
    public List<Biome> Biomes => biomes;


    [SerializeField] private LODThresholds lodThresholds = new();
    public LODThresholds LODThresholds => lodThresholds;
}