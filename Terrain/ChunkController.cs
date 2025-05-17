using System.Collections.Generic;
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
    public Dictionary<int, ChunkRenderData> ChunkData = new Dictionary<int, ChunkRenderData>();

    /// <summary>
    /// The LOD for this specific chunk to render.
    /// </summary>
    public int LOD {  get; set; }

    private void Awake()
    {
        // Add a foliage generator too.
        if (this.GetComponent<FoliageGenerator>() == null)
            this.AddComponent<FoliageGenerator>();
    }

    private void Start()
    {
        // Set the shader and material for this controller.
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Shader Graphs/VertexColor"));
        meshRenderer.material.SetFloat("_Smoothness", 0f);
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
        this.LOD = lodIndex;
    }

    /// <summary>
    /// Reset the controller back to its default state so that another controller could be set.
    /// </summary>
    public void ResetController()
    {
        Debug.Log("Reset");

        // Properties.
        this.chunkManager = null;
        this.Coordinates = default;
        this.ChunkData = new Dictionary<int, ChunkRenderData>();

        // Components.
        this.GetComponent<MeshFilter>().mesh = null;
        this.GetComponent<MeshCollider>().sharedMaterial = null;
    }

    /// <summary>
    /// Update the chunk data on this controller.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="mesh"></param>
    public void ApplyChunkData(ChunkRenderData renderData)
    {
        try
        {
            this.LOD = renderData.LOD;
            this.ChunkData[renderData.LOD] = renderData;

            this.GetComponent<MeshFilter>().mesh = renderData.Mesh;
            this.GetComponent<MeshCollider>().sharedMesh = renderData.LOD == 0 ? renderData.Mesh : null;

            //this.GetComponent<FoliageGenerator>().ApplyMap(renderData);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }
}