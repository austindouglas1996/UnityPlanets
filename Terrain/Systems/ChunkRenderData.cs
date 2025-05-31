
using System;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public enum ChunkRenderType
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
        RenderType = ChunkRenderType.GPU;
    }

    public ChunkRenderData(ChunkController controller, ChunkData data)
    {
        this.Controller = controller;
        this.Data = data;
        this.LocalToWorld = controller.transform.localToWorldMatrix;
        this.RenderType = ChunkRenderType.GameObject;
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

    public bool ShouldDestroy = false;

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
            if (RenderType == ChunkRenderType.GPU)
                this.Data.MeshData = null;

            return mesh;
        }
    }
    private Mesh mesh;
    public Matrix4x4 LocalToWorld {  get; set; }
    public ChunkRenderType RenderType { get; set; }
    public ChunkController? Controller { get; private set; }

    public void SetController(ChunkController controller)
    {
        this.Controller = controller;
    }

    public int LOD => Data?.Context.LODIndex ?? -1;
}