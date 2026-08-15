namespace GingerVoxelSystem
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Systems.Generation;
    using GingerVoxelSystem.Systems.Rendering;
    using UnityEngine;

    /// <summary>
    /// Unity-facing entry point for terrain generation.
    /// Ties together chunk layout, rendering, and a chosen generator (default: marching cubes).
    /// </summary>
    [RequireComponent(typeof(IChunkGenerator))]
    [RequireComponent(typeof(ChunkRendererMono))]
    public class TerrainGeneratorMono : MonoBehaviour, IChunkServices
    {
        [Tooltip("Configuration for terrain generation.")]
        public ChunkConfiguration ChunkConfiguration;

        protected ChunkRendererMono chunkRenderer;
        protected IChunkGenerator chunkGenerator;

        private ChunkMath cmath;
        private ChunkEditStore edits;

        /// <summary>
        /// Set up layout and renderer, and pick the generator backend.
        /// </summary>
        protected virtual void Awake()
        {
            chunkGenerator = GetComponent<IChunkGenerator>();
            chunkRenderer = GetComponent<ChunkRendererMono>();

            if (ChunkConfiguration == null)
                Debug.LogError("ChunkConfiguration is not assigned.");

            chunkRenderer.Initialize(this);
            cmath = new ChunkMath(this.ChunkConfiguration);
            edits = new ChunkEditStore(this);
        }

        /// <summary>
        /// Called when values change in the inspector.
        /// Pushes updated options into the generator and refreshes chunks.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying || chunkGenerator == null)
                return;

            chunkGenerator.UpdateOptions();
            chunkRenderer.RefreshChunks();
        }

        /// <summary>
        /// Inspector helper: repopulates the density options with the recommended
        /// "ordinary land + gentle hills" preset. Right-click the component header (or use the
        /// ⋮ menu) → "Reset To Ordinary Land". Engine dimensions and tuned globals (Seed,
        /// ISOLevel, WorldHeightAmplitude, PositionOffset) are preserved; only the shaping
        /// layers are reset. Pushes the change live when playing.
        /// </summary>
        [ContextMenu("Reset To Ordinary Land")]
        private void ResetToOrdinaryLand()
        {
            if (ChunkConfiguration == null)
            {
                Debug.LogWarning("ChunkConfiguration is not assigned; nothing to reset.");
                return;
            }

#if UNITY_EDITOR
            // Record before mutating so the reset is undoable in the editor.
            UnityEditor.Undo.RecordObject(this, "Reset To Ordinary Land");
#endif

            ChunkConfiguration.ResetDensityToOrdinaryLand();

            // Push the change live if we're already generating.
            if (Application.isPlaying && chunkGenerator != null)
            {
                chunkGenerator.UpdateOptions();
                chunkRenderer.RefreshChunks();
            }

#if UNITY_EDITOR
            // Mark dirty so the scene saves the reset when edited outside play mode.
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        IChunkConfiguration IChunkServices.Configuration => ChunkConfiguration;
        IChunkGenerator IChunkServices.Generator => chunkGenerator;
        ChunkLodOctree IChunkServices.Octree => chunkRenderer.LODTree;
        ChunkMath IChunkServices.CMath => cmath;
        ChunkEditStore IChunkServices.EditStore => edits;
    }
}