using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private List<BiomeOptions> biomeOptions = new List<BiomeOptions>();
    [SerializeField] private List<ITerrainModifier> modifiers = new();
    private List<IBiome> biomes = new List<IBiome>();

    public int ChunkSize => chunkSize;
    public DensityMapOptions MapOptions => densityMapOptions;
    public List<IBiome> Biomes => biomes;
    public List<ITerrainModifier> Modifiers => modifiers;

    public void Setup()
    {
        if (biomeOptions.Count == 0)
            throw new ArgumentNullException("No biomes.");

        IBiomeFactory biomeFactory = new GenericBiomeFactory(densityMapOptions, biomeOptions);
        foreach (var biome in biomeOptions)
        {
            Biomes.Add(biomeFactory.CreateInstance(biome.Name));
        }

        this.modifiers.Add(new PathModifer());
    }
}