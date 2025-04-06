using UnityEngine;

public class PlanetChunkColorizer : IChunkColorizer
{
    private Planet planet;

    public PlanetChunkColorizer(Planet planet)
    {
        this.planet = planet;
    }

    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        PlanetChunkConfiguration sphereConfig = ((PlanetChunkConfiguration)configuration);

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);
            float distance = (worldPos - planet.Center).magnitude;

            float normalized = Mathf.InverseLerp(configuration.MapOptions.StartSurfaceLevel, planet.PlanetRadius, distance);
            Color vertexColor = configuration.MapOptions.SurfaceColorRange.Evaluate(normalized);
            colors[i] = vertexColor;
        }

        return colors;
    }
}