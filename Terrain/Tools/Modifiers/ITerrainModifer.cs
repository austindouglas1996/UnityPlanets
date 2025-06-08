using UnityEngine;

public interface ITerrainModifier
{
}

public interface IModifyColor : ITerrainModifier
{
    void ModifyColor(ref Color32[] vertexColors, MeshData meshData, Matrix4x4 localToWorld);
}

public interface IModifyFoliageMask : ITerrainModifier
{
    void ModifyFoliageMask(ref float[,,] mask, Vector3Int coordinates);
}