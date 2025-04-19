using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public class LandMassChunkColorizer : IChunkColorizer
{
    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration config)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);
            colors[i] = config.BiomeMap.EvaluateColor(worldPos);
        }

        return colors;
    }
}