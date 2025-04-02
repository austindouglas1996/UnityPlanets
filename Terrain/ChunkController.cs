using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static PlanetChunk;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkController : MonoBehaviour
{
    public Vector3Int Coordinates;
    public IChunkConfiguration Configuration;
    public ChunkData ChunkData;

    public int RenderDetail { get; set; } = -1;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private BaseMarchingCubeGenerator generator;

    private void Awake()
    {
        this.name = $"Chunk X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";

        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        meshRenderer.material.SetFloat("_Smoothness", 0f);
    }

    public void Initialize(IChunkConfiguration config)
    {
        this.Configuration = config;
        if (this.Configuration.ChunkType == ChunkType.Sphere)
        {
            Planet planet = ((SphereChunkConfiguration)this.Configuration).Planet;
            this.generator = new SphereDensityMapGenerator(planet.Center, planet.Radius, planet.MapOptions);
        }
    }

    public async Task UpdateChunkAsync(bool initial = true)
    {
        Color[] colors = null;
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        await Task.Run(async () =>
        {
            if (initial || ChunkData == null)
            {
                ChunkData.DensityMap = generator.Generate(Configuration.ChunkSize, this.Coordinates);
                ChunkData.MeshData = generator.GenerateMeshData(ChunkData.DensityMap, new Vector3(0, 0, 0));
                ChunkData = new ChunkData(ChunkData.DensityMap, ChunkData.MeshData);
            }

            ChunkData.MeshData = generator.GenerateMeshData(ChunkData.DensityMap, new Vector3(0, 0, 0));

            colors = ApplyVertexColor(ChunkData.MeshData, localToWorld);
        });

        Mesh newMesh = generator.GenerateMesh(ChunkData.MeshData);

        newMesh.colors = colors;

        this.GetComponent<MeshFilter>().mesh = newMesh;
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;
    }

    /// <summary>
    /// Set the chunk visible. Helps with knowing whether to continue to render/update content.
    /// </summary>
    /// <param name="visible"></param>
    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

    private Color[] ApplyVertexColor(MeshData meshData, Matrix4x4 localToWorld)
    {
        Color[] colors = new Color[ChunkData.MeshData.Vertices.Count];

        if (Configuration.ChunkType == ChunkType.Sphere)
        {
            SphereChunkConfiguration sphereConfig = ((SphereChunkConfiguration)Configuration);

            for (int i = 0; i < ChunkData.MeshData.Vertices.Count; i++)
            {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(ChunkData.MeshData.Vertices[i]);
                float distance = (worldPos - sphereConfig.Planet.Center).magnitude;

                /*
                 * 
                 * THIS WAS PREVIOUSLY
                 * sphereConfig.Planet.StartSurfaceColorRadius, sphereConfig.Planet.EndSurfaceColorRadius
                 * 
                 * We changed this to 0f - 1f
                 */
                float normalized = Mathf.InverseLerp(0f, 1f, distance);
                Color vertexColor = sphereConfig.Planet.MapOptions.SurfaceColorRange.Evaluate(normalized);
                colors[i] = vertexColor;
            }
        }

        return colors;
    }
}