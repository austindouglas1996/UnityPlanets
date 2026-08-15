#ifndef CHUNK_TRIPLANAR_INCLUDED
#define CHUNK_TRIPLANAR_INCLUDED

// ============================================================================
// ChunkTriplanar
//
// World-space triplanar helpers for procedural Marching Cubes terrain, which has
// no authored UVs. All functions are PURE (textures + sampler + params are passed
// in), so they don't depend on global declaration order and stay reusable.
//
// Coordinates are ABSOLUTE world space, so textures are continuous across chunk
// boundaries (no per-chunk restart, no seams) and negative coordinates tile
// correctly. The three planar projections are blended by the world normal.
// ============================================================================

// Projection weights from the world normal. `sharpness` biases the blend toward
// the dominant axis so flat ground isn't a mushy 3-way average.
float3 TriplanarWeights(float3 worldNormal, float sharpness)
{
    float3 w = pow(abs(worldNormal), sharpness);
    return w / max(w.x + w.y + w.z, 1e-5);   // normalized → no unexpected brighten/darken
}

// Triplanar albedo: one sample per axis, blended by the projection weights.
float3 SampleTriplanarAlbedo(Texture2D tex, SamplerState samp, float3 wpos, float3 triW, float scale)
{
    float3 cx = SAMPLE_TEXTURE2D(tex, samp, wpos.zy * scale).rgb;
    float3 cy = SAMPLE_TEXTURE2D(tex, samp, wpos.xz * scale).rgb;
    float3 cz = SAMPLE_TEXTURE2D(tex, samp, wpos.xy * scale).rgb;
    return cx * triW.x + cy * triW.y + cz * triW.z;
}

// Triplanar normal map → world-space normal (swizzle / whiteout blend).
// Costs three extra samples, so only call this when a layer actually uses a map.
float3 SampleTriplanarNormal(Texture2D tex, SamplerState samp, float3 wpos, float3 worldNormal, float3 triW, float scale, float strength)
{
    float3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, wpos.zy * scale), strength);
    float3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, wpos.xz * scale), strength);
    float3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, wpos.xy * scale), strength);

    // Reorient each tangent-space normal onto the geometric normal, then blend.
    nx = float3(nx.xy + worldNormal.zy, worldNormal.x);
    ny = float3(ny.xy + worldNormal.xz, worldNormal.y);
    nz = float3(nz.xy + worldNormal.xy, worldNormal.z);

    return normalize(nx.zyx * triW.x + ny.xzy * triW.y + nz.xyz * triW.z);
}

// Paint one terrain layer over the running surface with a blend `weight`.
// Layers are combined by successive lerp, so weights are normalized by
// construction — the surface can't over-brighten or vanish. The whole layer
// (including its up-to-6 texture samples) is skipped when it isn't contributing.
void AddTerrainLayer(
    inout float3 albedo, inout float3 normalWS, inout float smoothness, inout float metallic,
    float weight,
    Texture2D albedoTex, Texture2D normalTex, SamplerState samp,
    float3 tint, float scale, float texStrength, float useTex,
    float useNormal, float normalStrength, float layerSmoothness, float layerMetallic,
    float3 wpos, float3 geomNormalWS, float3 triW)
{
    UNITY_BRANCH
    if (weight <= 0.001)
        return;

    // Albedo: solid tint, optionally modulated by the triplanar texture.
    // texStrength = 0 → pure solid color; 1 → full texture over tint.
    float3 c = tint;
    UNITY_BRANCH
    if (useTex > 0.5)
    {
        float3 t = SampleTriplanarAlbedo(albedoTex, samp, wpos, triW, scale);
        c = tint * lerp(float3(1.0, 1.0, 1.0), t, texStrength);
    }

    // Normal: geometric normal unless the layer supplies a normal map.
    float3 n = geomNormalWS;
    UNITY_BRANCH
    if (useNormal > 0.5)
        n = SampleTriplanarNormal(normalTex, samp, wpos, geomNormalWS, triW, scale, normalStrength);

    albedo     = lerp(albedo, c, weight);
    normalWS   = normalize(lerp(normalWS, n, weight));
    smoothness = lerp(smoothness, layerSmoothness, weight);
    metallic   = lerp(metallic, layerMetallic, weight);
}

#endif
