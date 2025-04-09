using Unity.VisualScripting;
using UnityEngine;

public class LandMassChunkColorizer : IChunkColorizer
{
    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        LandMassChunkConfiguration landConfig = ((LandMassChunkConfiguration)configuration);

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);

            float normalized = Mathf.InverseLerp(landConfig.SurfaceMin, landConfig.SurfaceMax, worldPos.y);
            Color vertexColor = configuration.MapOptions.SurfaceColorRange.Evaluate(normalized);
            colors[i] = vertexColor;
        }

        return colors;
    }
}