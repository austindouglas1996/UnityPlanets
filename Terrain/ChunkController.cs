using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Manages a single terrain chunk in the marching cubes system. Handles initialization, mesh generation,
/// terrain modification, color application, and optional foliage setup. Expected to be attached to each
/// chunk GameObject in the scene.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkController : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private IChunkGenerator generator;
    private IChunkColorizer colorizer;

    public Dictionary<int, ChunkData> ChunkData = new Dictionary<int, ChunkData>();
    private IChunkConfiguration Configuration;
    public Vector3Int Coordinates;

    /// <summary>
    /// Tells whether this chunk needs to be regenerated.
    /// </summary>
    private bool IsDirty = true;

    /// <summary>
    /// Tells whether this chunk is currently busy processing.
    /// </summary>
    private bool IsBusy = false;

    /// <summary>
    /// Tells whether this chunk has been rendered at least once with its chunk data assigned.
    /// </summary>
    public bool RenderedOnce = false;

    /// <summary>
    /// Tells whether the initial <see cref="Initialize(IChunkGenerator, IChunkColorizer, IChunkConfiguration, Vector3Int)"/> function has been called.
    /// </summary>
    private bool IsInitialized = false;

    /// <summary>
    /// Token used to help with cancelling async processes.
    /// </summary>
    private CancellationToken cancellationToken;

    /// <summary>
    /// The LOD for this specific chunk to render.
    /// </summary>
    public int LODIndex
    {
        get { return this.lodIndex; }
        set { this.lodIndex = value; this.IsDirty = true; }
    }
    private int lodIndex = 0;

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
            await UpdateChunkAsync(true, cancellationToken);
        }
    }

    /// <summary>
    /// Initialize the base components used throughout this controller.
    /// </summary>
    /// <param name="generator">Generator to generate the chunk data.</param>
    /// <param name="colorizer">Colorizer used to color the mesh (Vertex instance shader required)</param>
    /// <param name="config">Configuration used for mesh noise.</param>
    /// <param name="coordinates">Coordinates of this chunk.</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    public void Initialize(IChunkGenerator generator, IChunkColorizer colorizer, IChunkConfiguration config, Vector3Int coordinates, int lodIndex, CancellationToken cancellationToken = default)
    {
        if (coordinates != null)
        {
            this.Coordinates = coordinates; 
            this.name = $"Chunk X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";
        }

        if (config == null)
        {
            throw new System.ArgumentNullException("Configuration is null.");
        }

        this.generator = generator;
        this.colorizer = colorizer;
        this.Configuration = config;
        this.LODIndex = lodIndex;

        this.cancellationToken = cancellationToken;

        this.IsInitialized = true;
        this.IsDirty = true;
    }

    /// <summary>
    /// Builds or updates the mesh for this chunk, applies vertex colors, and optionally generates foliage if it's a new chunk.
    /// </summary>
    /// <param name="initial">True if this is the first time the chunk is being generated (e.g. foliage should be applied).</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task UpdateChunkAsync(bool initial = true, CancellationToken cancellationToken = default)
    {
        if (IsBusy || !IsInitialized) return;

        bool initializeFoliage = false;

        ChunkData chunkData;

        if (!this.ChunkData.TryGetValue(this.LODIndex, out chunkData))
        {
            chunkData = await ChunkGenerationQueue.Instance.Enqueue(() => generator.GenerateNewChunk(Coordinates, this.LODIndex, Configuration, cancellationToken));
            initializeFoliage = true;
        }
        else
        {
            await ChunkGenerationQueue.Instance.Enqueue(() => generator.UpdateChunkData(chunkData, Configuration, cancellationToken));
        }

        // No use continuing.
        if (chunkData.MeshData.Vertices.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        Mesh newMesh = generator.GenerateMesh(chunkData, Configuration);

        cancellationToken.ThrowIfCancellationRequested();

        this.GetComponent<MeshFilter>().mesh = newMesh;
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;

        this.ChunkData[this.LODIndex] = chunkData;

        ApplyChunkColors();

        if (initializeFoliage)
        {
            await this.GetComponent<FoliageGenerator>().ApplyMap(chunkData);
        }

        IsBusy = false;
        RenderedOnce = true;
    }

    /// <summary>
    /// Takes the existing density map and modifies the data based on the brush.
    /// </summary>
    /// <param name="brush">The brush that is used to modify the terrain.</param>
    /// <param name="isAdding">Whether we are removing or adding terrain.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task ModifyChunk(TerrainBrush brush, bool isAdding, CancellationToken token = default)
    {
        await this.generator.ModifyChunkData(this.ChunkData[this.LODIndex], this.Configuration, brush, this.Coordinates, isAdding, token);

        this.IsDirty = true;
    }

    /// <summary>
    /// Generates and applies and the colors on the generated mesh. This is needed to show
    /// proper colors on the mesh, but does require a specific vertex color shader to work.
    /// </summary>
    public void ApplyChunkColors()
    {
        Color[] colors = null;

        this.cancellationToken.ThrowIfCancellationRequested();

        ChunkData chunkData = this.ChunkData[this.LODIndex];

        if (chunkData.MeshData.Vertices.Count > 0)
        {
            Matrix4x4 matrix = transform.localToWorldMatrix;
            colors = colorizer.ApplyColors(chunkData.MeshData, matrix, chunkData.SurfaceMap, Configuration);

            foreach (ITerrainModifier modifier in Configuration.Modifiers)
            {
                if(modifier is IModifyColor colorMod)
                    colorMod.ModifyColor(ref colors, chunkData.MeshData, matrix, Configuration);
            }
        }
        else
            return;

        chunkData.VerticeColors = colors.ToArray();
        this.GetComponent<MeshFilter>().mesh.colors = chunkData.VerticeColors;

        this.ChunkData[this.LODIndex] = chunkData;
    }
}