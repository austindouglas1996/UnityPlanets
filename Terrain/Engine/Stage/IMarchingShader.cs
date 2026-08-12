namespace GingerVoxelSystem.Engine.Stage
{
    using GingerVoxelSystem.Systems.Rendering;
    using System;

    public interface IMarchingShader : IDisposable
    {
        void DispatchTriangleCount(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset);
        void DispatchMarching(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset);
    }
}
