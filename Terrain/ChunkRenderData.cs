using UnityEngine;

public class ChunkRenderData
{
    public ChunkRenderData(Vector3Int coordinates, ChunkData data, Mesh mesh, Matrix4x4 localToWorld)
    {
        Coordinates = coordinates;
        Data = data;
        Mesh = mesh;
        LocalToWorld = localToWorld;
    }

    public Vector3Int Coordinates {  get; set; }
    public ChunkData Data { get; set; }
    public Mesh Mesh { get; set; }
    public Matrix4x4 LocalToWorld {  get; set; }

    public int LOD => Data?.MeshData?.LODIndex ?? -1;

    public bool IsRenderable => Mesh != null && Data != null && Data.IsRenderable;
}