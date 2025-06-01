using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A helper class to help with drawing large batches of entities. Items like grass, flowers, rocks rendering will increase using an instance like this.
/// Objects along with their respected LOD groups are batched into groups. The groups are then rendered directly to the GPU jumping over Unity rendering 
/// system which greatly improves performance with large batch jobs with batches going into the hundred thousands, like with rendering grass in a field.
/// </summary>
public class MeshBatchDrawer
{
    private struct MeshDrawItem
    {
        public Mesh Mesh;
        public int SubMeshIndex;
        public Material Material;
        public List<Matrix4x4> Positions;
        public List<Vector4> Colors;
    }

    private Dictionary<GameObject, List<MeshLOD>> Meshes = new();
    private Dictionary<Vector3Int, MeshBatch> Batches = new();
    private List<MeshDrawItem> DrawList = new();

    private Quaternion LastFollowerRotation;

    /// <summary>
    /// Initialize a new instance of the <see cref="MeshBatchDrawer"/>.
    /// </summary>
    /// <param name="go"></param>
    /// <param name="follower"></param>
    public MeshBatchDrawer(Camera follower)
    {
        _Instance = this;
        this.Follower = follower;
    }

    public static MeshBatchDrawer Instance
    {
        get { return _Instance; }
        private set { _Instance = value; }
    }
    private static MeshBatchDrawer _Instance;

    /// <summary>
    /// The camera object the LOD objects should be chosen based on. Something like <see cref="Camera.main"/>
    /// </summary>
    public Camera Follower
    {
        get { return this._Follower; }
        set { this._Follower = value; }
    }
    private Camera _Follower;

    /// <summary>
    /// Gets or sets the material to override LOD materials. This is a helpful function if you want to change the default
    /// material of each LOD group to something specific.
    /// </summary>
    public Material MaterialOverride
    {
        get { return this._MaterialOverride; }
        set { this._MaterialOverride = value; }
    }
    private Material _MaterialOverride;

    /// <summary>
    /// Add a new position into the batch.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="scale"></param>
    public void Add(ChunkController controller, GameObject go, Vector3 position, Quaternion rotation, Vector3 scale, Color customColor)
    {
        float distanceToFollower = Vector3.Distance(position, Follower.transform.position);
        int lodIndex = GetLODIndex(distanceToFollower);

        // Make sure we have seen this gameObject before.
        int meshIndex = -1;
        if (!Meshes.ContainsKey(go))
        {
            this.Meshes.Add(go, MeshLOD.Extract(go));
            meshIndex = this.Meshes.Count - 1;
        }
        else
            meshIndex = this.Meshes.Keys.ToList().IndexOf(go); // NEED TO FIND A BETTER WAY OF THIS.

        if (!this.Batches.TryGetValue(controller.ChunkContext.Coordinates, out MeshBatch batch))
        {
            batch = new MeshBatch(controller.ChunkContext.Coordinates, controller.RenderData.Tree.Bounds);
            this.Batches[controller.ChunkContext.Coordinates] = batch;
        }

        batch.Add(meshIndex, position, rotation, scale, customColor);
    }

    /// <summary>
    /// Remove a region.
    /// </summary>
    /// <param name="controller"></param>
    public void Remove(Vector3Int coordinates)
    {
        this.Batches.Remove(coordinates);
    }

    /// <summary>
    /// Update and render the mesh instances. This method should be called every update frmae.
    /// </summary>
    public void Update()
    {
        float deltaAngle = Quaternion.Angle(LastFollowerRotation, Follower.transform.rotation);
        if (deltaAngle >= 10f)
        {
            this.UpdateDrawList();
            this.LastFollowerRotation = Follower.transform.rotation;
        }

        this.RenderInstances();
    }

    /// <summary>
    /// Render the instances of each active batch using <see cref="Graphics.DrawMeshInstanced(Mesh, int, Material, List{Matrix4x4})"/>. This method is a bit more
    /// efficent than native Unity rendering as we will automatically use a FrustumPlane to determine what should be rendered.
    /// </summary>
    private void RenderInstances()
    {
        int count = 0;

        foreach (var drawItem in this.DrawList)
        {
            if (drawItem.Positions.Count < 50)
                continue;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetVectorArray("_InstanceColor", drawItem.Colors.ToArray());

            Graphics.DrawMeshInstanced(drawItem.Mesh, drawItem.SubMeshIndex, drawItem.Material, drawItem.Positions, props);

            count += drawItem.Positions.Count;
        }

        Debug.Log($"Drawn {count}");
    }

    /// <summary>
    /// Update the list of entries to draw each frame. This list should only be updated when the follower moves some distance, or rotates by an angle.
    /// </summary>
    private void UpdateDrawList()
    {
        this.DrawList.Clear();

        // I use the main camera a lot, I keep breaking it when I turn off the player object.
        if (Camera.main == null) return;

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        Vector3 followerPosition = Follower.transform.position;

        foreach (var batch in this.Batches.Values)
        {
            if (!batch.InView(frustumPlanes, followerPosition))
                continue;

            var distance = Vector3.Distance(batch.RegionBounds.center, followerPosition);
            int lodIndex = GetLODIndex(distance);

            foreach (KeyValuePair<int, MeshBatchItem> entry in batch.Entries)
            {
                if (entry.Value.MeshIndex == -1)
                    throw new System.ArgumentException("Mesh LOD index is invalid.");

                var meshVariants = Meshes.ElementAt(entry.Value.MeshIndex).Value;
                int meshLodIndex = meshVariants.Count - 1 < lodIndex ? 0 : lodIndex;
                var meshLod = meshVariants[meshLodIndex];
                Mesh desiredMesh = meshLod.Mesh;

                var entryPositions = entry.Value.Positions;
                var entryColors = entry.Value.Colors;

                int added = 0;
                while (added < entryPositions.Count)
                {
                    // Try to find an existing item with space
                    MeshDrawItem item = this.DrawList.FirstOrDefault(
                        r => r.Mesh == desiredMesh &&
                             r.Material == meshLod.Mat &&
                             r.Positions.Count < 1023);

                    if (item.Mesh == null)
                    {
                        item = new MeshDrawItem
                        {
                            Mesh = desiredMesh,
                            SubMeshIndex = 0,
                            Material = meshLod.Mat,
                            Positions = new List<Matrix4x4>(),
                            Colors = new List<Vector4>()
                        };

                        this.DrawList.Add(item);
                    }

                    int spaceLeft = 1023 - item.Positions.Count;
                    int toAdd = Math.Min(spaceLeft, entryPositions.Count - added);

                    item.Positions.AddRange(entryPositions.GetRange(added, toAdd));
                    item.Colors.AddRange(entryColors.GetRange(added, toAdd));
                    added += toAdd;
                }
            }
        }
    }

    /// <summary>
    /// Retrieve the LOD index based on the distance.
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    private int GetLODIndex(float distance)
    {
        if (distance < 20)
            return 0;
        else if (distance < 30)
            return 1;

        return 2;
    }
}