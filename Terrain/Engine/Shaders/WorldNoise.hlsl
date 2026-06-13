#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Lib/PerlinNoise.hlsl"

// ─────────────────────────────────────────────────────────────────────────────
// GenerateNoiseValue
//
// Returns a signed density value for a given world-space position.
//
//   positive → air (above the surface)
//   negative → solid (below the surface)
//
// All feature heights are now driven directly by cbuffer parameters,
// so each value represents actual world units:
//
//   MacroHeight     — vertical range of plains variation (e.g. 20)
//   HillHeight      — max height of hills in world units (e.g. 80)
//   MountainHeight  — max height of mountains in world units (e.g. 300)
//   MountainCoverage— 0..1 fraction of land that becomes mountainous (e.g. 0.25)
//
// Frequencies are also driven by cbuffer:
//   MacroFreq       — continent scale (very low, e.g. 0.0004)
//   HillFreq        — hill detail scale (e.g. 0.002)
//   MountainFreq    — mountain region and ridge scale (e.g. 0.0015)
//   DetailFreq      — fine surface variation (e.g. 0.005)
//
// WorldHeightAmplitude acts as a final global multiplier.
// With per-feature heights in world units, set this to 1.0 for direct
// control, or use it as a global vertical scale override.
//
// ─────────────────────────────────────────────────────────────────────────────
float GenerateNoiseValue(float3 p)
{
    float2 uv = p.xz;

    // ── 1. Continental shelf ──────────────────────────────────────────────────
    //
    // MacroFreq drives continent scale directly. At typical terrain scales
    // this should be much lower than HillFreq or DetailFreq so that
    // landmasses span many chunks.
    float continent = N01(fbm2D(uv * MacroFreq, 5));

    // landMask:  0 = open ocean, 1 = land interior
    // coastMask: wider taper for blending ocean floor depth near shorelines
    float landMask = smoothstep(0.42, 0.58, continent);
    float coastMask = smoothstep(0.30, 0.50, continent);


    // ── 2. Mountain region mask ───────────────────────────────────────────────
    //
    // MountainFreq controls the spatial scale of mountain ranges.
    // MountainCoverage [0..1] drives how much of the land becomes mountainous:
    //   0.0 = no mountains
    //   0.5 = mountains on roughly half the land
    //   1.0 = mountains nearly everywhere
    //
    // The threshold is inverted (1 - coverage) so higher coverage = lower bar
    // to qualify as a mountain region.
    float regionNoise = N01(fbm2D(uv * MountainFreq + float2(47.3, 91.7), 4));
    float coverageThresh = 1.0 - saturate(MountainCoverage);
    float mountainMask = smoothstep(coverageThresh, coverageThresh + 0.20, regionNoise);
    mountainMask = mountainMask * mountainMask; // square: makes mountain regions rarer and more defined
    mountainMask *= landMask; // mountains never appear in the ocean


    // ── 3. Plains base ────────────────────────────────────────────────────────
    //
    // DetailFreq adds fine-scale rolling variation to flat land.
    // MacroHeight sets the vertical range in world units — keep this small
    // (e.g. 10..30) for subtle plains undulation.
    float plainsNoise = N01(fbm2D(uv * DetailFreq + float2(13.1, 37.4), 3));
    float plainsHeight = landMask
                       * (1.0 - mountainMask)
                       * lerp(0.0, MacroHeight, plainsNoise);


    // ── 4. Hills ─────────────────────────────────────────────────────────────
    //
    // HillFreq and HillHeight both come from the cbuffer.
    // HillHeight is in world units (e.g. 80 = hills up to 80 units tall).
    // Squaring hillNoise biases toward flat ground with occasional bumps.
    // Partially suppressed in mountain zones (0.7 factor) so hill detail
    // can still bleed into mountain bases.
    float hillNoise = N01(fbm2D(uv * HillFreq + float2(72.8, 5.3), 4));
    hillNoise = hillNoise * hillNoise;
    float hillHeight = landMask
                     * (1.0 - mountainMask * 0.7)
                     * hillNoise * HillHeight;


    // ── 5. Mountains ──────────────────────────────────────────────────────────
    //
    // MountainHeight is in world units (e.g. 300 = mountains up to 300 units).
    // Domain warping uses MountainFreq scale for consistency with the region mask.
    // pow(1.8) sharpens peaks: values near 1 stay near 1, mid-range drops off,
    // producing distinct summits rather than smooth domes.
    float2 warpA = float2(
        fbm2D(uv * MountainFreq + float2(3.2, 88.1), 3),
        fbm2D(uv * MountainFreq + float2(55.6, 19.4), 3)
    );
    float2 warpedUV = uv + warpA * 0.35;

    float mountainNoise = N01(fbm2D(warpedUV * (MountainFreq * 0.75), 6));
    mountainNoise = pow(mountainNoise, 1.8);
    float mountainHeight = mountainMask * mountainNoise * MountainHeight;


    // ── 6. Ocean floor ────────────────────────────────────────────────────────
    //
    // Depth is proportional to MacroHeight so oceans scale with land.
    // coastMask tapers depth to zero near shorelines to avoid a hard cliff
    // at the waterline.
    float oceanNoise = N01(fbm2D(uv * MacroFreq + float2(28.4, 66.2), 3));
    float oceanFloor = lerp(-MacroHeight * 2.0, -MacroHeight * 0.5, oceanNoise);
    float oceanHeight = (1.0 - coastMask) * oceanFloor;


    // ── Combine ───────────────────────────────────────────────────────────────
    //
    // Each feature contributes in world units via its cbuffer height parameter.
    // WorldHeightAmplitude is a final global scale — set to 1.0 to use feature
    // heights directly, or tune it as a single vertical override across all features.
    //
    // Suggested starting values:
    //   MacroHeight       = 20    (plains vary 0..20 units)
    //   HillHeight        = 80    (hills up to 80 units)
    //   MountainHeight    = 300   (mountains up to 300 units)
    //   MountainCoverage  = 0.25  (mountains on ~25% of land)
    //   WorldHeightAmplitude = 1.0
    float landHeight = plainsHeight + hillHeight + mountainHeight;
    float finalHeight = lerp(oceanHeight, landHeight, landMask);

    return p.y - (finalHeight * WorldHeightAmplitude);
}

#endif
