using SingularityGroup.HotReload;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages a single terrain chunk in the marching cubes system. Handles initialization, mesh generation,
/// terrain modification, color application, and optional foliage setup. Expected to be attached to each
/// chunk GameObject in the scene.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkController : MonoBehaviour
{
    public Vector3Int Coordinates;
    private ChunkManager chunkManager;

    /// <summary>
    /// A collection of <see cref="ChunkData"/> based on LOD index for easy rendering.
    /// </summary>
    public Dictionary<int, ChunkData> ChunkData = new Dictionary<int, ChunkData>();

    /// <summary>
    /// Tells whether this chunk needs to be regenerated.
    /// </summary>
    private bool IsDirty = true;

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
        set 
        {
            // This should not be an issue, but it happened once.
            if (value == this.lodIndex)
                return;

            this.lodIndex = value; 
            this.IsDirty = true; 
        }
    }
    private int lodIndex = 0;

    private void Awake()
    {
        // Set the shader and material for this controller.
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        meshRenderer.material.SetFloat("_Smoothness", 0f);

        // Add a foliage generator too.
        if (this.GetComponent<FoliageGenerator>() == null)
            this.AddComponent<FoliageGenerator>();
    }

    private void Update()
    {
        if (IsDirty)
        {
            IsDirty = false;

            // Do we need to regenerate the chunk?
            if (!this.ChunkData.TryGetValue(this.LODIndex, out ChunkData chunkData))
            {
                this.chunkManager.RequestNewChunkGeneration(this);
            }
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
    public void Initialize(ChunkManager manager, Vector3Int coordinates, int lodIndex, CancellationToken cancellationToken = default)
    {
        this.Coordinates = coordinates;
        this.name = $"Chunk X:{Coordinates.x} Y:{Coordinates.y} Z:{Coordinates.z}";

        this.chunkManager = manager;
        this.LODIndex = lodIndex;

        this.cancellationToken = cancellationToken;
        this.IsDirty = true;
    }

    /// <summary>
    /// Reset the controller back to its default state so that another controller could be set.
    /// </summary>
    public void Reset()
    {
        // Properties.
        this.chunkManager = null;
        this.Coordinates = default;
        this.ChunkData = new Dictionary<int, ChunkData>();
        this.cancellationToken = default;
        this.IsDirty = false;

        // Components.
        this.GetComponent<MeshFilter>().mesh = null;
        this.GetComponent<MeshRenderer>().material = null;
        this.GetComponent<MeshCollider>().sharedMaterial = null;
    }

    /// <summary>
    /// Update the chunk data on this controller.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="mesh"></param>
    public void ApplyChunkData(ChunkData data, Mesh mesh)
    {
        this.ChunkData[data.MeshData.LODIndex] = data;

        this.GetComponent<MeshFilter>().mesh = mesh;
        this.GetComponent<MeshCollider>().sharedMesh = data.MeshData.LODIndex == 0 ? mesh : null;
        
        this.GetComponent<FoliageGenerator>().ApplyMap(data, cancellationToken);
    }
}