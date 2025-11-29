namespace GingerVoxelSystem.EditorSupport
{
    using System;
    using UnityEngine;

    /// <summary>
    /// General height category of the biome. Mainly used to separate underwater from land biomes.
    /// Most biomes will end up in AboveWater.
    /// </summary>
    public enum BiomeHeight
    {
        Debug = -1,
        BelowWater = 0,
        WaterLevel = 1,
        AboveWater = 2
    }

    /// <summary>
    /// Temperature category of the biome. This is the second major factor
    /// (after height) when deciding which biome to use.
    /// </summary>
    public enum BiomeTemperature
    {
        Debug = -1,
        Cold = 0,
        Normal = 1,
        Warm = 2,
        Hot = 3,
    }

    /// <summary>
    /// Humidity level of the biome. Used to tell apart things like deserts, plains,
    /// swamps, and jungles.
    /// </summary>
    public enum BiomeHumidity
    {
        Debug = -1,
        Dry = 0,
        Normal = 1,
        Wet = 2
    }

    /// <summary>
    /// Extra tie-breaker for biome selection. Foliage density helps split
    /// similar climates into open areas (plains), mixed areas (grasslands),
    /// or covered areas (forests/jungles).
    /// </summary>
    public enum BiomeFoliage
    {
        Debug = -1,
        None = 0,
        Sparse = 1,
        Dense = 2
    }

    [Serializable]
    public class Biome
    {
        public Biome()
        {

        }

        public Biome(string name, BiomeHeight height, BiomeTemperature temperature, BiomeHumidity humidity, BiomeFoliage foliage)
        {
            Name = name;
            Height = height;
            Temperature = temperature;
            Humidity = humidity;
            Foliage = foliage;
        }

        public string Name;
        public BiomeHeight Height;
        public BiomeTemperature Temperature;
        public BiomeHumidity Humidity;
        public BiomeFoliage Foliage;

        public Color Highlight;
        public Color Light;
        public Color MidLight;
        public Color Mid;
        public Color Dark;
        public Color Shadow;
    }
}