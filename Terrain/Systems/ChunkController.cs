using System;
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
    public ChunkContext ChunkContext;
    public ChunkOctTree Tree;

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

        Material mat = new Material(Shader.Find("Shader Graphs/VertexColor"));
        mat.SetFloat("_Smoothness", 0f);

        meshRenderer.sharedMaterial = mat;
    }

    /// <summary>
    /// Initialize the base components used throughout this controller.
    /// </summary>
    /// <param name="generator">Generator to generate the chunk data.</param>
    /// <param name="colorizer">Colorizer used to color the mesh (Vertex instance shader required)</param>
    /// <param name="config">Configuration used for mesh noise.</param>
    /// <param name="coordinates">Coordinates of this chunk.</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    public void Initialize(ChunkContext context, CancellationToken cancellationToken = default)
    {
        this.ChunkContext = context;
        this.transform.position = context.WorldPosition;
    }

    /// <summary>
    /// Reset the controller back to its default state so that another controller could be set.
    /// </summary>
    public void ResetController()
    {
        try
        {
            // Properties.
            this.ChunkContext = default;
            this.Tree = null;

            // Destroy
            Destroy(this.GetComponent<MeshFilter>().mesh);
            Destroy(this.GetComponent<MeshCollider>().sharedMesh);

            // Components.
            this.GetComponent<MeshFilter>().mesh = null;
            this.GetComponent<MeshCollider>().sharedMaterial = null;
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private void OnDrawGizmos()
    {
        if (this.ChunkContext == null) return;

        Gizmos.color = Color.white;
        DrawBoundsRecursive(Tree);
    }

    private void DrawBoundsRecursive(ChunkOctTree node)
    {
        if (node.RenderData != null)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, node.LODIndex / 4f);
            Gizmos.DrawWireCube(node.Bounds.center, node.Bounds.size);
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child != null)
                    DrawBoundsRecursive(child);
            }
        }
    }

    private void Update()
    {
        if (this.Tree != null && this.Tree.RenderData == null)
        {
            this.ChunkContext.Services.ControllerFactory.Release(this);
        }
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
            this.Tree = renderData.Tree;
            var Coordinates = this.ChunkContext.Coordinates;
            this.name = this.ChunkContext.ToString();
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