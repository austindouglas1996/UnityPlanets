namespace MarchingTerrain.Core
{
    using MarchingTerrain.Systems.Generation;

    /// <summary>
    /// Runtime facade for chunk world state and generation.
    /// Provides configuration, layout, and generation services used by
    /// rendering and non-rendering systems alike. Intentionally render-agnostic.
    /// </summary>
    public interface IChunkServices
    {
        /// <summary>
        /// Immutable settings that control chunk sizing/behavior.
        /// </summary>
        IChunkConfiguration Configuration { get; }

        /// <summary>
        /// Provides helpful math functions for handling chunk data.
        /// </summary>
        ChunkMath CMath { get; }

        /// <summary>
        /// A greedy system for managing what chunks should be rendered throughout the world.
        /// </summary>
        ChunkLodOctree Octree { get; }

        /// <summary>
        /// Produces the data needed to build chunk meshes/fields.
        /// </summary>
        IChunkGenerator Generator { get; }

        /// <summary>
        /// Provides a store of chunk edits.
        /// </summary>
        ChunkEditStore EditStore { get; }
    }
}