using UnityEngine;

public class MicroChunkColorizer : IChunkColorizer
{
    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            colors[i] = new Color(92,64,51);
        }

        return colors;
    }
}