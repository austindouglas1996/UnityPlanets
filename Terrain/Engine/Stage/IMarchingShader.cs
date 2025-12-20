using GingerVoxelSystem.Systems.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Terrain.Engine.Stage
{
    public interface IMarchingShader : IDisposable
    {
        void DispatchTriangleCount(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset);
        void DispatchMarching(ChunkRenderBatch batch, int groupsX, int groupsY, int groupsZ, int offset);
    }
}
