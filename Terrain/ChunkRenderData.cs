using UnityEngine;

public enum ChunkRenderType
{
    GameObject,
    GPU
}

public class ChunkRenderData
{
    public ChunkRenderData(Vector3Int coordinates, ChunkData data, Mesh mesh, Matrix4x4 localToWorld)
    {
        Coordinates = coordinates;
        Data = data;
        Mesh = mesh;
        LocalToWorld = localToWorld;
        RenderType = ChunkRenderType.GPU;
    }

    public ChunkRenderData(ChunkController controller, ChunkData data, Mesh mesh)
    {
        this.Controller = controller;
        this.Coordinates = controller.Coordinates;
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

    public Vector3Int Coordinates {  get; set; }
    public ChunkData Data { get; set; }
    public Mesh Mesh { get; set; }
    public Matrix4x4 LocalToWorld {  get; set; }
    public ChunkRenderType RenderType { get; set; }

    public ChunkController? Controller { get; set; }

    public int LOD => Data?.MeshData?.LODIndex ?? -1;
}