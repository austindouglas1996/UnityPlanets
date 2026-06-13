namespace GingerVoxelSystem.Engine.Options
{
    using System;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Global override for how cold/hot the worlds feels overall.
    /// Shifts how often colder/hotter biomes (like tundras or volcanos) appear.
    /// </summary>
    public enum TemperatureBias
    {
        ExtremelyCold = -2,
        Cold = -1,
        Normal = 0,
        Hot = +1,
        ExtremelyHot = +2
    }

    /// <summary>
    /// Global override for how wet the world feels overall.
    /// Shifts how often wetter biomes (like swamps) appear.
    /// </summary>
    public enum HumidityBias
    {
        VeryDry = -2,
        Dry = -1,
        Normal = 0,
        Wet = 1,
        VeryWet = 2,
    }

    /// <summary>
    /// Global override for how dense vegetation appears.
    /// Doesn’t directly control trees, but affects how much *can* spawn.
    /// </summary>
    public enum FoliageBias
    {
        None = -2,
        VerySparse = -1,
        Normal = 0,
        Rich = 1,
        VeryRich = 2
    }

    /// <summary>
    /// Global water bias controls how much land is underwater.
    /// Useful for creating more or less coastline without changing terrain height.
    /// </summary>
    public enum WaterBias
    {
        DriedUp = -2,
        Low = -1,
        Normal = 0,
        High = 1,
        Flooded = 2
    }

    /// <summary>
    /// First/pass base terrain layer. Shapes the big landmass, sprinkles broad detail,
    /// and modulates detail with a flatness mask. Later layers (rivers, caves, mountains)
    /// can stack on top.
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct TerrainDensityOptions
    {
        [Header("Global")]
        [Tooltip("Logical voxel width of a chunk (before LOD).")]
        public int CellsPerAxis;

        [Tooltip("World-space size of a voxel at LOD0. Increasing this makes triangles larger and reduces geometric detail.")]
        [Range(1,32)]
        public int BaseCellStep;

        [Tooltip("Additional voxel width given for sampling for edge smoothness.")]
        public int BorderSamplesPerAxis;

        [Tooltip("The noise seed, use this to get a different set of generation")]
        public int Seed;

        [Tooltip("Iso threshold used by marching. You better have a good reason for changing this.")]
        [Range(-1f, 1f)]
        public float ISOLevel;

        [Tooltip("Controls the additional space given to each world unit.")]
        public float WorldHeightAmplitude;


        [Header("Climate")]
        [Tooltip("Overall temperature of the generation.")]
        public TemperatureBias TemperatureBias;

        [Tooltip("Overall humidity of the generation.")]
        public HumidityBias HumidityBias;

        [Tooltip("Overall foliage of the generation.")]
        public FoliageBias FoliageBias;

        [Tooltip("Adjusts water level (lower = less ocean, higher = more ocean).")]
        public WaterBias SeaLevelBias;


        [Header("Macro Land")]
        [Tooltip("Controls the size of major landmasses. Lower = larger continents, higher = smaller fragmented terrain.")]
        public float MacroFreq;

        [Tooltip("Overall height of large-scale terrain. Affects how tall land rises above sea level.")]
        public float MacroHeight;

        [Tooltip("Normalized water threshold (0–1). Higher values create more ocean coverage.")]
        [Range(0f, 1f)]
        public float SeaLevel;

        [Header("Hills")]
        [Tooltip("Frequency of rolling hills layered on top of the macro terrain.")]
        public float HillFreq;

        [Tooltip("Vertical strength of hill variation.")]
        public float HillHeight;

        [Header("Mountains")]
        [Tooltip("Frequency of mountain structures. Lower values create broader ranges.")]
        public float MountainFreq;

        [Tooltip("Maximum vertical strength of mountains.")]
        public float MountainHeight;

        [Tooltip("Controls how often mountains appear across the world.")]
        [Range(0f, 1f)]
        public float MountainCoverage;

        [Header("Detail")]
        [Tooltip("Small-scale surface variation to prevent flat areas from looking artificial.")]
        public float DetailFreq;

        [Tooltip("Strength of fine terrain noise.")]
        public float DetailHeight;

        [Header("Domain Offsets")]
        [Tooltip("XYZ offset for generation. A simple way to control the position as generation happens at 0,0,0.")]
        public Vector3 PositionOffset;

        [Tooltip("XYZ offset for base landmass noise domain.")]
        public Vector3 BaseOffset;

        [Tooltip("XYZ offset for detail domain (replaces +1234).")]
        public Vector3 DetailOffset;

        [Tooltip("XYZ offset for flatness domain (replaces +5555).")]
        public Vector3 FlatMaskOffset;
    }
}