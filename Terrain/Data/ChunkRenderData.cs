
using System;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public enum ChunkRenderState
{
    GameObject,
    GPU
}

public class ChunkRenderData
{
    public ChunkRenderData(ChunkContext context, ChunkData data, Matrix4x4 localToWorld)
    {
        Context = context;
        Data = data;
        LocalToWorld = localToWorld;
        State = ChunkRenderState.GPU;
    }

    public ChunkRenderData(ChunkController controller, ChunkData data)
    {
        this.Controller = controller;
        this.Data = data;
        this.LocalToWorld = controller.transform.localToWorldMatrix;
        this.State = ChunkRenderState.GameObject;
    }

    public bool IsActive
    {
        get { return isActive; }
        set
        {
            isActive = value;
            if (this.Controller != null)
                this.Controller.gameObject.SetActive(value);
        }
    }
    private bool isActive = true;

    public bool CanRenderGPU
    {
        get { return this.State == ChunkRenderState.GPU && this.isActive; }
    }

    public Vector3Int Coordinates => Context.Coordinates;

    public ChunkContext Context { get; set; }
    public ChunkData Data { get; set; }
    public ChunkOctTree Tree { get; set; }
    public Mesh Mesh
    {
        get
        {
            if (mesh == null)
                mesh = Data.GenerateMesh();

            // Free resources.
            if (State == ChunkRenderState.GPU)
                this.Data.MeshData = null;

            return mesh;
        }
    }
    private Mesh mesh;
    public Matrix4x4 LocalToWorld {  get; set; }
    public ChunkRenderState State { get; set; }
    public ChunkController? Controller { get; private set; }

    public void SetController(ChunkController controller)
    {
        this.Controller = controller;
    }

    public int LOD => Data?.Context.LODIndex ?? -1;
}