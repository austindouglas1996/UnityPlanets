namespace GingerVoxelSystem.Engine
{
    using System;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Subvarient types for better rendering.
    /// </summary>
    public enum TerrainType
    {
        /// <summary>
        /// Endless land, no roof or floor.
        /// </summary>
        Terrain,

        /// <summary>
        /// A sphere of land, no roof but has a center.
        /// </summary>
        Planet,

        /// <summary>
        /// Cave systems, roof and floor.
        /// </summary>
        Cave
    }

    /// <summary>
    /// Global override for how cold/hot the worlds feels overall.
    /// Shifts how often colder/hotter biomes (like tundras or volcanos) appear.
    /// </summary>
    public enum TemperatureBias
    {
        ExtremelyCold = -4,
        Cold = -2,
        Normal = 0,
        Hot = 2,
        ExtremelyHot = 4
    }

    /// <summary>
    /// Global override for how wet the world feels overall.
    /// Shifts how often wetter biomes (like swamps) appear.
    /// </summary>
    public enum HumidityBias
    {
        VeryDry = -4,
        Dry = -2,
        Normal = 0,
        Wet = 2,
        VeryWet = 4,
    }

    /// <summary>
    /// Global override for how dense vegetation appears.
    /// Doesn’t directly control trees, but affects how much *can* spawn.
    /// </summary>
    public enum FoliageBias
    {
        None = -4,
        VerySparse = -2,
        Normal = 0,
        Rich = 2,
        VeryRich = 4
    }

    /// <summary>
    /// Global water bias controls how much land is underwater.
    /// Useful for creating more or less coastline without changing terrain height.
    /// </summary>
    public enum WaterBias
    {
        DriedUp = -4,
        Low = -2,
        Normal = 0,
        High = 2,
        Flooded = 4
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
        public int CubesPerAxis;

        [Tooltip("Additional voxel width given for sampling for edge smoothness.")]
        public int BorderSamplesPerAxis;

        [Tooltip("The noise seed, use this to get a different set of generation")]
        public int Seed;

        [Tooltip("Those chosen generation type. Each type has unique features or classification.")]
        public TerrainType TerrainType;

        [Tooltip("Iso threshold used by marching. You better have a good reason for changing this.")]
        [Range(-1f, 1f)]
        public float ISOLevel;


        [Header("Climate")]
        [Tooltip("Overall temperature of the generation.")]
        public TemperatureBias TemperatureBias;

        [Tooltip("Overall humidity of the generation.")]
        public HumidityBias HumidityBias;

        [Tooltip("Overall foliage of the generation.")]
        public FoliageBias FoliageBias;

        [Tooltip("Adjusts water level (lower = less ocean, higher = more ocean).")]
        public WaterBias SeaLevelBias;


        [Header("Continents")]
        [Tooltip("How wide the continents are. Lower = bigger continents.")]
        public float ContinentFreq;

        [Tooltip("How much the base layer lifts terrain up.")]
        public float ContinentHeight;

        [Tooltip("Global height scale for this layer after normalization.")]
        public float ContinentAmp;

        [Tooltip("Gives each continent (on land) with its own freqency to reduce copies")]
        public float ContinentAmpFreq;

        [Tooltip("The amount of Octaves to be used in FBM (Increases details at cost of processing)"), Range(1, 12)]
        public uint ContinentOctaves;


        [Header("Oceans & Coasts")]
        [Tooltip("Land/ocean split. Higher = more ocean. Typical 0.45–0.6.")]
        [Range(0f, 1f)]
        public float SeaLevel;

        [Tooltip("Width of the coast transition. Smaller = sharper coastlines.")]
        [Range(0f, 1f)]
        public float CoastWidth;

        [Tooltip("How far below baseline oceans are carved. Negative values = carve downward.")]
        public float OceanDepth;

        /// <summary>
        /// DETAIL & FLAT DO NOT DO ANYTHING, REWORK LATER.
        /// </summary>
        [Header("Broad Detail")]
        [Tooltip("Size of large-scale bumps on top of the base.")]
        public float DetailFreq;

        [Tooltip("Strength of those broad details.")]
        public float DetailAmp;

        [Header("Flatness Mask")]
        [Tooltip("How large the flat regions run across the map.")]
        public float FlatMaskFreq;

        [Tooltip("Higher -> stronger flattening in masked zones.")]
        public float FlatMaskAmp;

        [Header("Coloring")]
        public float ColorSampleRadius;

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