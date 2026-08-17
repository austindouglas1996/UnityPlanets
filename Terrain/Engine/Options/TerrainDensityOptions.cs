namespace MarchingTerrain.Engine.Options
{
    using System;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Simple on/off switch for an optional terrain layer. Backed by <see cref="int"/>
    /// so it stays blittable for the density constant buffer (0 = off, 1 = on) while
    /// still showing as a tidy dropdown in the inspector.
    /// </summary>
    public enum FeatureToggle
    {
        Disabled = 0,
        Enabled = 1
    }

    /// <summary>
    /// Layered terrain-shaping configuration consumed by the density compute kernels.
    ///
    /// The terrain is a height field built from independent layers:
    ///   Base land (always on) + Hills + Mountains − Lakes, then optionally blended
    ///   down into Oceans. Each optional layer can be toggled off with no effect on
    ///   the rest, and every field below maps 1:1 to <c>ChunkCBuffer.hlsl</c>.
    ///
    /// LAYOUT CONTRACT: this struct is uploaded raw as an HLSL cbuffer, so the field
    /// order, types, and 16-byte packing must stay identical to ChunkCBuffer.hlsl.
    /// It is intentionally all 4-byte scalars plus one <see cref="Vector3"/> aligned
    /// to a 16-byte boundary and padded to 160 bytes. Do not reorder or retype fields
    /// without updating the shader to match.
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct TerrainDensityOptions
    {
        [Header("Global")]
        [Tooltip("Logical voxel width of a chunk (before LOD).")]
        public int CellsPerAxis;

        [Tooltip("World-space size of a voxel at LOD0. Larger = bigger triangles, less detail.")]
        [Range(1, 32)]
        public int BaseCellStep;

        [Tooltip("Extra voxel width sampled per side for seamless chunk edges.")]
        public int BorderSamplesPerAxis;

        [Tooltip("World seed. Shifts every layer's noise domain, so a new seed = a new world.")]
        public int Seed;

        [Tooltip("Marching iso threshold. The surface forms where density == this value. Leave at 0 unless you know why.")]
        [Range(-1f, 1f)]
        public float ISOLevel;

        [Tooltip("Global vertical scale applied to the whole height field. 1.0 = per-layer heights are literal world units.")]
        public float WorldHeightAmplitude;


        [Header("Base Land (always on)")]
        [Tooltip("Reference ground height in world units. The stable level the whole world sits around.")]
        public float BaseElevation;

        [Tooltip("Scale of the base undulation. Low = broad, gentle swells.")]
        public float BaseFrequency;

        [Tooltip("How far the base ground rises and dips around BaseElevation (world units).")]
        public float BaseAmplitude;

        [Tooltip("Domain nudge for the base layer. Change to reshuffle just the base shape.")]
        public float BaseSeedOffset;


        [Header("Hills")]
        [Tooltip("Rolling hills layered on top of the base land.")]
        public FeatureToggle HillsEnabled;

        [Tooltip("Scale of the hills. Higher = smaller, more frequent hills.")]
        public float HillFrequency;

        [Tooltip("Maximum hill rise in world units.")]
        public float HillHeight;

        [Tooltip("0..1. Higher keeps more land flat and makes hills rarer / gentler.")]
        [Range(0f, 1f)]
        public float HillThreshold;

        [Tooltip("Domain nudge for the hills layer.")]
        public float HillSeedOffset;


        [Header("Mountains")]
        [Tooltip("Rare, tall mountain regions.")]
        public FeatureToggle MountainsEnabled;

        [Tooltip("Scale of the region mask that decides WHERE ranges sit. Low = broad ranges.")]
        public float MountainRangeFrequency;

        [Tooltip("Scale of the peak detail inside a range.")]
        public float MountainDetailFrequency;

        [Tooltip("Maximum peak rise in world units.")]
        public float MountainHeight;

        [Tooltip("0..1 fraction of land that becomes mountainous.")]
        [Range(0f, 1f)]
        public float MountainCoverage;

        [Tooltip("Edge softness of the mountain region (transition from flat land into range).")]
        [Range(0.01f, 0.5f)]
        public float MountainRangeSoftness;

        [Tooltip("Peak sharpening. 1 = rounded domes, higher = sharper summits.")]
        [Range(1f, 6f)]
        public float MountainSharpness;

        [Tooltip("Domain nudge for the mountains layer.")]
        public float MountainSeedOffset;


        [Header("Lakes")]
        [Tooltip("Localized basins carved into the land.")]
        public FeatureToggle LakesEnabled;

        [Tooltip("Scale of the lakes. Higher = smaller, more numerous basins.")]
        public float LakeFrequency;

        [Tooltip("How far a lake basin carves down (world units).")]
        public float LakeDepth;

        [Tooltip("0..1. Keep HIGH (~0.7) so lakes stay isolated pools instead of merging into channels.")]
        [Range(0f, 1f)]
        public float LakeThreshold;

        [Tooltip("Shore blend width of a lake edge.")]
        [Range(0.01f, 0.5f)]
        public float LakeEdgeSoftness;

        [Tooltip("Domain nudge for the lakes layer.")]
        public float LakeSeedOffset;


        [Header("Oceans")]
        [Tooltip("Broad seas driven by a continental mask. The only layer that takes terrain below zero.")]
        public FeatureToggle OceansEnabled;

        [Tooltip("Continental scale. Very low = large continents and seas.")]
        public float ContinentFrequency;

        [Tooltip("0..1 sea-level threshold on the continent field. Higher = more ocean coverage.")]
        [Range(0f, 1f)]
        public float SeaLevelThreshold;

        [Tooltip("Shoreline blend width. Wider = gentler beaches.")]
        [Range(0.01f, 0.5f)]
        public float CoastSoftness;

        [Tooltip("Sea-floor height in world units (usually negative).")]
        public float OceanFloorHeight;

        [Tooltip("Domain nudge for the ocean/continent layer.")]
        public float OceanSeedOffset;


        // Alignment padding: keeps PositionOffset on a 16-byte boundary for the
        // constant buffer. Not user-facing; do not remove or reorder.
        [HideInInspector] public float _Pad0;

        [Header("World / Material")]
        [Tooltip("XYZ world offset. Pushed to the render material (does not affect the density field).")]
        public Vector3 PositionOffset;

        // Tail padding → 160-byte struct (a multiple of 16). Do not remove.
        [HideInInspector] public float _Pad1;


        /// <summary>
        /// Recommended starting configuration: ordinary, gently rolling land with hills on and
        /// mountains / lakes / oceans off. Pass in your existing engine dimensions so this never
        /// overwrites the values the terrain pipeline depends on.
        ///
        /// Heights are in world units and frequencies are per world unit, tuned for a world where
        /// a voxel is roughly one world unit. Frequencies are independent of BaseCellStep (they
        /// act on world-space position), so a larger BaseCellStep just samples the same shapes
        /// more coarsely — feature sizes stay put.
        /// </summary>
        public static TerrainDensityOptions CreateOrdinaryLand(int cellsPerAxis, int baseCellStep, int borderSamplesPerAxis)
        {
            return new TerrainDensityOptions
            {
                // Global (engine dimensions come from the caller; do not guess these)
                CellsPerAxis = cellsPerAxis,
                BaseCellStep = baseCellStep,
                BorderSamplesPerAxis = borderSamplesPerAxis,
                Seed = 0,
                ISOLevel = 0f,
                WorldHeightAmplitude = 1f,

                // Base land — ground sits near y=8 and gently rolls a few units.
                BaseElevation = 8f,
                BaseFrequency = 0.004f,   // ~250-unit swells
                BaseAmplitude = 4f,
                BaseSeedOffset = 0f,

                // Hills (on) — broad rolling rises up to ~24 units.
                HillsEnabled = FeatureToggle.Enabled,
                HillFrequency = 0.010f,   // ~100-unit hills
                HillHeight = 24f,
                HillThreshold = 0.30f,    // most land gently hilly
                HillSeedOffset = 0f,

                // Mountains (off, but tuned so flipping them on looks good).
                MountainsEnabled = FeatureToggle.Disabled,
                MountainRangeFrequency = 0.0012f,  // ~830-unit ranges
                MountainDetailFrequency = 0.012f,  // ~83-unit peaks
                MountainHeight = 90f,
                MountainCoverage = 0.18f,
                MountainRangeSoftness = 0.15f,
                MountainSharpness = 2.5f,
                MountainSeedOffset = 0f,

                // Lakes (off, pre-tuned) — isolated basins, high threshold.
                LakesEnabled = FeatureToggle.Disabled,
                LakeFrequency = 0.020f,   // ~50-unit pools
                LakeDepth = 8f,
                LakeThreshold = 0.72f,
                LakeEdgeSoftness = 0.08f,
                LakeSeedOffset = 0f,

                // Oceans (off, pre-tuned) — broad seas, soft coast.
                OceansEnabled = FeatureToggle.Disabled,
                ContinentFrequency = 0.0008f,  // ~1250-unit continents
                SeaLevelThreshold = 0.42f,
                CoastSoftness = 0.09f,
                OceanFloorHeight = -14f,
                OceanSeedOffset = 0f,

                PositionOffset = Vector3.zero,
            };
        }
    }
}
