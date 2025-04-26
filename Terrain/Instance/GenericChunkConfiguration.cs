using System;
using System.Collections.Generic;
using UnityEngine;

public enum BiomesEnum
{
    DeepOcean = 0,
    Ocean = 1, 
    Beach = 2,
    Plains = 3,
    Mountain = 4,
}

[Serializable]
public class BiomeDensityOptions
{
    public BiomesEnum Biome;
    [SerializeField] public DensityMapOptions DensityMapOptions;
}

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private List<BiomeDensityOptions> biomeOptions = new List<BiomeDensityOptions>();
    [SerializeField] private List<ITerrainModifier> modifiers = new();

    public BiomeMap BiomeMap { get; private set; }
    public int ChunkSize => chunkSize;
    public DensityMapOptions MapOptions => densityMapOptions;
    public List<ITerrainModifier> Modifiers => modifiers;

    public void Setup()
    {
        if (biomeOptions.Count == 0)
            throw new ArgumentNullException("No biomes.");

        List<IBiome> biomes = new List<IBiome>();
        foreach (var biome in biomeOptions)
        {
            if (biome.Biome == BiomesEnum.DeepOcean)
                biomes.Add(new DeepOceanBiomeNoise(biome.DensityMapOptions));
            if (biome.Biome == BiomesEnum.Ocean)
                biomes.Add(new OceanBiomeNoise(biome.DensityMapOptions));
            if (biome.Biome == BiomesEnum.Beach)
                biomes.Add(new BeachBiomeNoise(biome.DensityMapOptions));
            if (biome.Biome == BiomesEnum.Plains)
                biomes.Add(new PlainBiomeNoise(biome.DensityMapOptions));
            if (biome.Biome == BiomesEnum.Mountain)
                biomes.Add(new MountainBiomeNoise(biome.DensityMapOptions));
        }
        
        this.BiomeMap = new BiomeMap(biomes);

        this.modifiers.Add(new PathModifer());
    }
}