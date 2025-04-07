using UnityEngine;

public class HeightDensityMapGenerator : BaseMarchingCubeGenerator
{
    public HeightDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override DensityMapOptions Options { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public override float[,,] Generate(int chunkSize, Vector3Int chunkCoordinates)
    {
        throw new System.NotImplementedException();
    }

    public override Mesh GenerateMesh(MeshData data)
    {
        throw new System.NotImplementedException();
    }
}