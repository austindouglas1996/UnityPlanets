using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

/// <summary>
/// A helper class to help with drawing large batches of entities. Items like grass, flowers, rocks rendering will increase using an instance like this.
/// Objects along with their respected LOD groups are batched into groups. The groups are then rendered directly to the GPU jumping over Unity rendering 
/// system which greatly improves performance with large batch jobs with batches going into the hundred thousands, like with rendering grass in a field.
/// </summary>
public class MeshBatchDrawer
{
    private const int MAX_BATCH_SIZE = 1023; // Unity's instance batch limit

    private class MeshBatchItem
    {
        public MeshBatchItem(int meshIndex)
        {
            this.MeshIndex = meshIndex;
        }

        public int MeshIndex;
        public List<Matrix4x4> Positions = new List<Matrix4x4>();
        public List<Vector4> Colors = new();
    }

    private struct MeshDrawItem
    {
        public Mesh Mesh;
        public int SubMeshIndex;
        public Material Material;
        public List<Matrix4x4> Positions;
        public List<Vector4> Colors;
    }

    private class MeshBatch
    {
        private int currentIndex = 0;
        private float lastSeenDistance = 0;

        public MeshBatch()
        {
            lastSeenDistance = float.MaxValue;
            Entries = new Dictionary<int, MeshBatchItem>();
        }

        public Dictionary<int, MeshBatchItem> Entries;
        public Bounds Bounds;

        public bool IsFull
        {
            get { return currentIndex >= 1023; }
        }

        public float FollowerDistance
        {
            get { return lastSeenDistance; }
        }

        public void Add(int meshIndex, Vector3 position, Quaternion rotation, Vector3 scale, Color customColor)
        {
            if (meshIndex == -1)
            {
                throw new System.ArgumentException("MeshIndex cannot be set to -1.");
            }

            if (!Entries.ContainsKey(meshIndex))
            {
                Entries.Add(meshIndex, new MeshBatchItem(meshIndex));
            }

            if (currentIndex == 1)
            {
                Bounds = new Bounds(position, Vector3.one * 5f);
            }

            MeshBatchItem currentBatch = Entries[meshIndex];
            currentBatch.Positions.Add(Matrix4x4.TRS(position, rotation, scale));
            currentBatch.Colors.Add(customColor);
            currentIndex++;

            Bounds.Encapsulate(position);
        }

        public bool InView(Plane[] frustumPlanes, Vector3 followerPosition)
        {
            if (this.currentIndex == 0)
                return false;

            // Calculate the distance between the batch's center and the follower's position
            float distanceToFollower = Vector3.Distance(Bounds.center, followerPosition);
            lastSeenDistance = distanceToFollower;
            if (distanceToFollower > 50f)
                return false;

            if (GeometryUtility.TestPlanesAABB(frustumPlanes, Bounds))
            {
                return true;
            }

            return false;
        }
    }

    private Dictionary<GameObject, List<MeshLOD>> Meshes = new Dictionary<GameObject, List<MeshLOD>>();
    private List<MeshBatch> Batches = new List<MeshBatch>();
    private List<MeshDrawItem> DrawList = new List<MeshDrawItem>();

    private Vector3 LastFollowerPosition;
    private Quaternion LastFollowerRotation;

    /// <summary>
    /// Initialize a new instance of the <see cref="MeshBatchDrawer"/>.
    /// </summary>
    /// <param name="go"></param>
    /// <param name="follower"></param>
    public MeshBatchDrawer(Camera follower)
    {
        this.Batches.Add(new MeshBatch());
        this.Follower = follower;
        this.LastFollowerPosition = follower.transform.position;
    }

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
    public void Add(GameObject go, Vector3 position, Quaternion rotation, Vector3 scale, Color customColor)
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

        // Grab the last batch, create a new batch if full.
        MeshBatch currentBatch = this.Batches.Last();
        if (currentBatch.IsFull)
        {
            currentBatch = new MeshBatch();
            this.Batches.Add(currentBatch);
        }

        currentBatch.Add(meshIndex, position, rotation, scale, customColor);
    }

    /// <summary>
    /// Update and render the mesh instances. This method should be called every update frmae.
    /// </summary>
    public void Update()
    {
        float deltaAngle = Quaternion.Angle(LastFollowerRotation, Follower.transform.rotation);
        if (deltaAngle >= 35f)
        {
            this.UpdateDrawList();
            this.LastFollowerRotation = Follower.transform.rotation;
        }

        this.RenderInstances();
    }

    /// <summary>
    /// Reset the distance from the last known follower position.
    /// </summary>
    public void UpdateFollowerPosition()
    {
        this.LastFollowerPosition = Follower.transform.position;
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

        for (int i = 0; i < Batches.Count; i++)
        {
            if (Batches[i].InView(frustumPlanes, followerPosition))
            {
                MeshBatch batch = Batches[i];
                int lodIndex = GetLODIndex(batch.FollowerDistance);

                foreach (KeyValuePair<int, MeshBatchItem> entry in batch.Entries)
                {
                    if (entry.Value.MeshIndex == -1)
                        throw new System.ArgumentException("Mesh LOD index is invalid.");

                    var mesh = Meshes.ElementAt(entry.Value.MeshIndex).Value;
                    int meshLodIndex = mesh.Count - 1 < lodIndex ? 0 : lodIndex;
                    var meshLod = mesh[meshLodIndex];

                    MeshDrawItem newItem = new MeshDrawItem();
                    newItem.Mesh = mesh[meshLodIndex].Mesh;
                    newItem.SubMeshIndex = 0;
                    newItem.Material = meshLod.Mat;
                    newItem.Positions = entry.Value.Positions;
                    newItem.Colors = entry.Value.Colors;

                    this.DrawList.Add(newItem);
                }
            }
        }
    }

    /// <summary>
    /// Render the instances of each active batch using <see cref="Graphics.DrawMeshInstanced(Mesh, int, Material, List{Matrix4x4})"/>. This method is a bit more
    /// efficent than native Unity rendering as we will automatically use a FrustumPlane to determine what should be rendered.
    /// </summary>
    private void RenderInstances()
    {
        foreach (var drawItem in this.DrawList)
        {
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetVectorArray("_InstanceColor", drawItem.Colors.ToArray());

            Graphics.DrawMeshInstanced(drawItem.Mesh, drawItem.SubMeshIndex, drawItem.Material, drawItem.Positions, props);
        }
    }

    /// <summary>
    /// Retrieve the LOD index based on the distance.
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    private int GetLODIndex(float distance)
    {
        return 1;

        if (distance < 20)
            return 0;
        else if (distance < 30)
            return 1;

        return 2;
    }
}