using Unity.VisualScripting;
using UnityEngine;

public class LandMassChunkColorizer : IChunkColorizer
{
    private BiomeNoise biomeNoise;
    public LandMassChunkColorizer()
    {
        biomeNoise = new BiomeNoise(new IBiome[] { new PlainBiome(0.4f, 0.1f, 52), new OceanBiome(depthScale: 0.4f, depthStrength: 20f, seed: 88f), new MountainBiome(5f, 60f, 52) }, 0.003f, 52);
    }

    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        LandMassChunkConfiguration landConfig = ((LandMassChunkConfiguration)configuration);

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);

            //float normalized = Mathf.InverseLerp(landConfig.SurfaceMin, landConfig.SurfaceMax, worldPos.y);
            //Color vertexColor = configuration.MapOptions.SurfaceColorRange.Evaluate(normalized);
            //colors[i] = vertexColor;
            colors[i] = biomeNoise.EvaluateColor(worldPos);
        }

        return colors;
    }
}