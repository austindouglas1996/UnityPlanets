
using System;
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
            this.Controller?.gameObject.SetActive(value);
        }
    }
    private bool isActive = true;

    public ChunkContext Context { get; set; }
    public ChunkData Data { get; set; }
    public Mesh Mesh
    {
        get
        {
            if (mesh == null)
                mesh = Data.GenerateMesh();

            return mesh;
        }
    }
    private Mesh mesh;
    public Matrix4x4 LocalToWorld {  get; set; }
    public ChunkRenderType RenderType { get; set; }

    public ChunkController? Controller { get; set; }

    public int LOD => Data?.Context.LODIndex ?? -1;
}