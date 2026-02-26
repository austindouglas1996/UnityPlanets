namespace GingerVoxelSystem.Systems.Generation
{
    using GingerVoxelSystem.Core;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Stores and organizes terrain edits in large world-space sectors (512x512 in XZ).
    /// 
    /// Edits are grouped by sector to reduce the number of edits checked per chunk.
    /// When a chunk regenerates, only edits from overlapping sectors are evaluated.
    /// </summary>
    public class ChunkEditStore
    {
        /// <summary>
        /// Internal wrapper for a single terrain edit.
        /// Stores the edit data and its world-space bounds for fast intersection testing.
        /// </summary>
        private class ChunkEditEntry
        {
            /// <summary>
            /// Creates a new edit entry and computes its bounding box.
            /// </summary>
            /// <param name="data">The edit data to store.</param>
            public ChunkEditEntry(ChunkEditData data)
            {
                this.Data = data;

                // Bounds are centered at the hit point and extend by radius in all directions.
                this.Bounds = new Bounds(data.HitPointWP, Vector3.one * data.Radius * 2);
            }

            /// <summary>
            /// Axis-aligned bounding box of the edit in world space.
            /// </summary>
            public readonly Bounds Bounds;

            /// <summary>
            /// The underlying edit data.
            /// </summary>
            public ChunkEditData Data;
        }

        private IChunkServices chunkServices;

        /// <summary>
        /// Maps sector coordinates (XZ) to the list of edits inside that sector.
        /// Each sector represents a 512x512 region in world space.
        /// </summary>
        private Dictionary<Vector2Int, List<ChunkEditEntry>> editEntries = new();

        /// <summary>
        /// Creates a new edit store.
        /// </summary>
        /// <param name="services">Chunk services used for bounds calculations.</param>
        public ChunkEditStore(IChunkServices services)
        {
            this.chunkServices = services;
        }

        /// <summary>
        /// Adds a new terrain edit at the given world position.
        /// 
        /// The edit is assigned to a sector based on its world-space XZ position.
        /// Returns the chunk keys that intersect this edit's bounds so they can be regenerated.
        /// </summary>
        /// <param name="hitPoint">World-space position of the edit.</param>
        /// <param name="operation">Edit operation type (e.g., add/remove).</param>
        /// <returns>List of chunk keys affected by this edit.</returns>
        public List<ChunkKey> Add(Vector3 hitPoint, int operation)
        {
            Vector2Int sector = SectorOf(hitPoint);

            if (!editEntries.TryGetValue(sector, out var list))
            {
                list = new List<ChunkEditEntry>();
                editEntries.Add(sector, list);
            }

            var entry = new ChunkEditEntry(new ChunkEditData(hitPoint, operation));
            list.Add(entry);

            return GetKeysFromBounds(entry.Bounds);
        }

        /// <summary>
        /// Clears all stored edits.
        /// </summary>
        public void Clear()
        {
            this.editEntries.Clear();
        }

        /// <summary>
        /// Queries edits affecting a specific chunk key.
        /// </summary>
        /// <param name="key">Chunk to query.</param>
        /// <param name="edits">List to populate with intersecting edits.</param>
        public void Query(ChunkKey key, ref List<ChunkEditData> edits)
        {
            Bounds bounds = chunkServices.CMath.GetBounds(key);
            this.Query(bounds, ref edits);
        }

        /// <summary>
        /// Queries edits whose bounds intersect the provided world-space bounds.
        /// 
        /// Determines which sectors overlap the bounds and checks edits inside them.
        /// </summary>
        /// <param name="bounds">World-space bounds to test against.</param>
        /// <param name="edits">List to populate with intersecting edits.</param>
        public void Query(Bounds bounds, ref List<ChunkEditData> edits)
        {
            // Determine sector range overlapped by these bounds (XZ only)
            int minRx = Mathf.FloorToInt(bounds.min.x / 512f);
            int maxRx = Mathf.FloorToInt(bounds.max.x / 512f);
            int minRz = Mathf.FloorToInt(bounds.min.z / 512f);
            int maxRz = Mathf.FloorToInt(bounds.max.z / 512f);

            for (int rx = minRx; rx <= maxRx; rx++)
            {
                for (int rz = minRz; rz <= maxRz; rz++)
                {
                    Vector2Int sector = new Vector2Int(rx, rz);

                    if (!editEntries.TryGetValue(sector, out var list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        var entry = list[i];
                        if (bounds.Intersects(entry.Bounds))
                            edits.Add(entry.Data);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the sector coordinate (XZ) for a world-space position.
        /// 
        /// Sectors are 512 units wide in both X and Z.
        /// </summary>
        /// <param name="p">World-space position.</param>
        /// <returns>Sector coordinate as Vector2Int.</returns>
        static Vector2Int SectorOf(Vector3 p)
        {
            return new Vector2Int(
                Mathf.FloorToInt(p.x / 512f),
                Mathf.FloorToInt(p.z / 512f));
        }

        /// <summary>
        /// Computes all chunk keys intersecting the provided bounds.
        /// 
        /// Used to determine which chunks must be regenerated after an edit.
        /// </summary>
        /// <param name="b">World-space bounds.</param>
        /// <returns>List of affected chunk keys (LOD 0).</returns>
        private List<ChunkKey> GetKeysFromBounds(Bounds b)
        {
            List<ChunkKey> keys = new();

            int chunkSize = chunkServices.Configuration.DensityOptions.CellsPerAxis;

            Vector3 min = b.min;
            Vector3 max = b.max;

            Vector3Int minChunk = new Vector3Int(
                Mathf.FloorToInt(min.x / chunkSize),
                Mathf.FloorToInt(min.y / chunkSize),
                Mathf.FloorToInt(min.z / chunkSize));

            Vector3Int maxChunk = new Vector3Int(
                Mathf.FloorToInt(max.x / chunkSize),
                Mathf.FloorToInt(max.y / chunkSize),
                Mathf.FloorToInt(max.z / chunkSize));

            for (int x = minChunk.x; x <= maxChunk.x; x++)
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                    for (int z = minChunk.z; z <= maxChunk.z; z++)
                        keys.Add(new ChunkKey(new Vector3Int(x, y, z), lod: 0));

            return keys;
        }
    }
}