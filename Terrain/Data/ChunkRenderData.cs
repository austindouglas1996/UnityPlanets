
using System;
using UnityEngine;

public enum ChunkRenderState
{
    GameObject,
    GPU
}

/// <summary>
/// Represents all the rendering-related data for a single chunk.
/// </summary>
public class ChunkRenderData
{
    private bool isActive;
    private Mesh mesh;

    public ChunkRenderData(ChunkContext context, ChunkData data, Matrix4x4 localToWorld)
    {
        this.Context = context;
        this.Data = data;
        this.LocalToWorld = localToWorld;
        this.State = ChunkRenderState.GPU;
        this.isActive = true;
    }

    public ChunkRenderData(ChunkController controller, ChunkData data)
    {
        this.Controller = controller;
        this.Data = data;
        this.Context = data.Context; // Pull context from data to ensure consistency
        this.LocalToWorld = controller.transform.localToWorldMatrix;
        this.State = ChunkRenderState.GameObject;
        this.isActive = true;
    }

    /// <summary>
    /// Whether this chunk is currently active in the scene.
    /// </summary>
    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            if (Controller != null)
                Controller.gameObject.SetActive(value);
        }
    }

    /// <summary>
    /// Whether this chunk is GPU-renderable and active.
    /// </summary>
    public bool CanRenderGPU => State == ChunkRenderState.GPU && IsActive;

    /// <summary>
    /// World matrix for positioning this chunk in the scene.
    /// </summary>
    public Matrix4x4 LocalToWorld { get; set; }

    /// <summary>
    /// The chunk’s logical context (coordinates, LOD, etc).
    /// </summary>
    public ChunkContext Context { get; private set; }

    /// <summary>
    /// The chunk’s generated terrain and mesh data.
    /// </summary>
    public ChunkData Data { get; private set; }

    /// <summary>
    /// Parent tree reference (used for merging, subdividing).
    /// </summary>
    public ChunkOctTree Tree { get; set; }

    /// <summary>
    /// The mesh used for rendering. Auto-generates from data if needed.
    /// </summary>
    public Mesh Mesh
    {
        get
        {
            if (mesh == null && Data != null)
            {
                mesh = Data.GenerateMesh();

                // Free memory early if only used by GPU.
                if (State == ChunkRenderState.GPU)
                    Data.MeshData = null;
            }

            return mesh;
        }
    }

    /// <summary>
    /// Current rendering method (GameObject vs. GPU).
    /// </summary>
    public ChunkRenderState State { get; private set; }

    /// <summary>
    /// Reference to the controller (if rendered using GameObject).
    /// </summary>
    public ChunkController? Controller { get; private set; }

    /// <summary>
    /// Shortcut for chunk coordinates.
    /// </summary>
    public Vector3Int Coordinates => Context.Coordinates;

    /// <summary>
    /// Shortcut for chunk LOD index.
    /// </summary>
    public int LOD => Context.LODIndex;

    /// <summary>
    /// Assign a controller to this chunk.
    /// </summary>
    public void SetController(ChunkController controller)
    {
        this.Controller = controller;
        this.State = ChunkRenderState.GameObject;
    }
}
