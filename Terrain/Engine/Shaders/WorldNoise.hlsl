#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "WorldShaping.hlsl"

// ─────────────────────────────────────────────────────────────────────────────
// GenerateNoiseValue
//
// Returns a signed density value for a world-space position:
//
//   positive → air    (above the surface)
//   negative → solid  (below the surface)
//
// The terrain is a HEIGHT FIELD: we build a surface height H(x,z) from a stack of
// independent layers, then return  p.y - H.  This form (unchanged from before)
// keeps marching cubes, normals, and collision behaving exactly as they always
// have — the surface is wherever p.y == H.
//
// Layer stack (each optional layer can be toggled off with zero effect):
//
//   Base land  (always on)  H  = BaseElevation + noise*BaseAmplitude
//   Hills      (+, ≥0)      H += hillMask   * HillHeight
//   Mountains  (+, ≥0)      H += regionMask * peaks * MountainHeight
//   Lakes      (−)          H -= basin      * LakeDepth
//   Oceans                  H  = lerp(H, OceanFloorHeight, seaMask)
//
// Design rules that keep terrain controllable and river-free:
//   • Base land never thresholds anything, so with every optional layer off you
//     still get ordinary, gently uneven ground — not a flat plane, never sunken.
//   • Hills and mountains are ADDITIVE and non-negative: they can only raise the
//     ground, so they cannot carve the winding trenches the old mask-lerp did.
//   • Lakes threshold noise near its HIGH end, which selects isolated round blobs
//     (basins) instead of the long winding contours a mid threshold produces.
//   • Oceans are the ONLY layer that lowers terrain below zero, and only through a
//     broad, low-frequency continental mask — so land never floods on its own.
// ─────────────────────────────────────────────────────────────────────────────
float GenerateNoiseValue(float3 p)
{
    float2 uv = p.xz;

    // ── Base land (always on) ─────────────────────────────────────────────────
    // Low-frequency, low-octave undulation around BaseElevation. Signed noise
    // (~[-1,1]) so the ground gently rises and dips a little around the reference
    // level. This is the stable foundation every other layer sits on.
    float baseN  = fbm2D(LayerCoord(uv, BaseFrequency, DOMAIN_BASE, BaseSeedOffset), 3);
    float height = BaseElevation + baseN * BaseAmplitude;

    // ── Hills (optional, additive) ────────────────────────────────────────────
    // smoothstep(HillThreshold, 1) keeps most ground flat and only lets the upper
    // range of the noise rise into hills, giving occasional broad mounds instead
    // of uniform waviness. Raising HillThreshold makes hills rarer and flatter land
    // more common.
    if (HillsEnabled != 0)
    {
        float h = N01(fbm2D(LayerCoord(uv, HillFrequency, DOMAIN_HILL, HillSeedOffset), 3));
        h = smoothstep(HillThreshold, 1.0, h);
        height += h * HillHeight;
    }

    // ── Mountains (optional, additive) ────────────────────────────────────────
    // Two parts: a low-frequency REGION mask decides *where* ranges are (rare),
    // and a higher-frequency detail field shapes the peaks inside those regions.
    // pow(peaks, MountainSharpness) lifts summits and rounds the valleys WITHOUT
    // the knife-edge ridges/ravines that abs()-style ridged noise creates.
    if (MountainsEnabled != 0)
    {
        float mask  = MountainRegionMask(uv);
        float peaks = N01(fbm2D(LayerCoord(uv, MountainDetailFrequency, DOMAIN_MTN_DETAIL, MountainSeedOffset), 4));
        peaks = pow(peaks, MountainSharpness);
        height += mask * peaks * MountainHeight;
    }

    // ── Lakes (optional, subtractive) ─────────────────────────────────────────
    // A HIGH threshold selects only the isolated peaks of the noise field — round
    // blobs, not the winding contours you get near a mid threshold. That is what
    // keeps lakes as localized basins rather than rivers. LakeDepth is how far the
    // basin carves down; LakeEdgeSoftness is the shore blend.
    if (LakesEnabled != 0)
    {
        float l = N01(fbm2D(LayerCoord(uv, LakeFrequency, DOMAIN_LAKE, LakeSeedOffset), 2));
        float basin = smoothstep(LakeThreshold, LakeThreshold + LakeEdgeSoftness, l);
        height -= basin * LakeDepth;
    }

    // ── Oceans (optional) ─────────────────────────────────────────────────────
    // Where the broad continental field falls below sea level, blend the whole
    // surface down toward OceanFloorHeight. A little of the base undulation is kept
    // so the sea floor isn't dead flat. This is the only layer that can take the
    // surface below zero, so plains never turn into water by accident.
    if (OceansEnabled != 0)
    {
        float seaMask = OceanMask(uv);
        float floorH  = OceanFloorHeight + baseN * (BaseAmplitude * 0.25);
        height = lerp(height, floorH, seaMask);
    }

    // Height-field density. WorldHeightAmplitude is a single global vertical scale
    // (1.0 = use the per-layer heights directly). Sign/form unchanged from before.
    return p.y - height * WorldHeightAmplitude;
}

#endif
