using UnityEngine;

public interface IChunkColorizer
{
    Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, IChunkConfiguration configuration);
}