using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    private bool IsDirty = true;
    private bool IsBusy = false;
    private bool IsInitialized = false;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        meshRenderer.material.SetFloat("_Smoothness", 0f);

        if (this.GetComponent<FoliageGenerator>() == null)
            this.AddComponent<FoliageGenerator>();
    }

    private async void Update()
    {
        if (IsDirty)
        {
            IsDirty = false;
            await UpdateChunkAsync();
        }
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

    public async Task UpdateChunkAsync(bool initial = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsBusy) return;

        if (!IsInitialized)
        {
            return;
        }

        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        if (ChunkData == null)
        {
            ChunkData = await generator.GenerateNewChunk(Coordinates, Configuration);
        }
        else
        {
            await generator.UpdateChunkData(ChunkData, Configuration);
        }

        // No use continuing.
        if (ChunkData.MeshData.Vertices.Count == 0)
            return;

        Mesh newMesh = generator.GenerateMesh(ChunkData, Configuration);

        this.GetComponent<MeshFilter>().mesh = newMesh;
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;

        ApplyChunkColors();
       
        //if (initial)
            //await this.GetComponent<FoliageGenerator>().ApplyMap(ChunkData);

        IsBusy = false;
    }

    public async Task ModifyChunk(TerrainBrush brush, bool addingOrSubtracting, CancellationToken token = default)
    {
        await this.generator.ModifyChunkData(this.ChunkData, this.Configuration, brush, this.Coordinates, addingOrSubtracting, token);

        this.IsDirty = true;
    }

    public void ApplyChunkColors()
    {
        Color[] colors = null;

        if (ChunkData.MeshData.Vertices.Count > 0)
        {
            colors = colorizer.ApplyColors(ChunkData.MeshData, transform.localToWorldMatrix, Configuration);
        }
        else
            return;

        ChunkData.VerticeColors = colors.ToArray();
        this.GetComponent<MeshFilter>().mesh.colors = ChunkData.VerticeColors;
    }
}