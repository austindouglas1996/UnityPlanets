using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkController : MonoBehaviour
{
    public Vector3Int Coordinates;
    public IChunkConfiguration Configuration;
    public ChunkData ChunkData;

    public int RenderDetail { get; set; } = -1;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private IChunkGenerator generator;
    private IChunkColorizer colorizer;

    private bool IsInitialized = false;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        meshRenderer.material.SetFloat("_Smoothness", 0f);
    }

    public void Initialize(IChunkGenerator generator, IChunkColorizer colorizer, IChunkConfiguration config, Vector3Int coordinates)
    {
        if (coordinates != null)
        {
            this.Coordinates = coordinates;
        }

        if (config == null)
        {
            throw new System.ArgumentNullException("Configuration is null.");
        }

        this.generator = generator;
        this.colorizer = colorizer;
        this.Configuration = config;

        this.name = $"Chunk X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";

        this.IsInitialized = true;
    }

    public async Task UpdateChunkAsync(bool initial = true)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("Tried to call UpdateChunkAsync() before initializing chunk.");
            return;
        }

        Color[] colors = null;
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        await Task.Run(async () =>
        {
            if (initial || ChunkData == null)
            {
                ChunkData = await generator.GenerateNewChunk(Coordinates, Configuration);
            }
            else
            {
                await generator.UpdateChunkData(ChunkData, Configuration);
            }

            if (ChunkData.MeshData.Vertices.Count > 0)
            {
                colors = colorizer.ApplyColors(ChunkData.MeshData, localToWorld, Configuration);
            }
        });

        // No use continuing.
        if (ChunkData.MeshData.Vertices.Count == 0)
            return;

        Mesh newMesh = generator.GenerateMesh(ChunkData, Configuration);

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
}