using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VHierarchy.Libs;

public class ChunkManager : MonoBehaviour
{
    public Transform Follower;
    public ChunkType ChunkType;
    public IChunkConfiguration Configuration;

    [Header("Rendering")]
    [Tooltip("How far a given chunk can be that it will be rendered on screen. Details will automatically be adjusted on distance.")]
    public float ChunkRenderDistance = 400;

    [Tooltip("How far the follower needs to be travel before we update the active chunks.")]
    public float TravelDistanceToUpdateChunks = 10f;

    private IChunkLayout Layout;
    private IChunkControllerFactory Factory;

    private Dictionary<Vector3Int, ChunkController> ActiveChunks = new Dictionary<Vector3Int, ChunkController>();
    private Dictionary<Vector3Int, ChunkController> CacheChunks = new Dictionary<Vector3Int, ChunkController>();

    private Vector3 LastKnownFollowerPosition;

    private bool IsBusy = false;

    private void Awake()
    {
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);

        if (this.ChunkType == ChunkType.Sphere)
        {
            this.Layout = new SphereChunkLayout();
        }
    }

    private async void Update()
    {
        if (IsFollowerOutsideOfRange())
        {
            await UpdateChunks();
        }
    }

    /// <summary>
    /// Debug function to help with quickly re-rendering the output.
    /// </summary>
    public void Restart()
    {
        this.ActiveChunks.Clear();
        this.CacheChunks.Clear();

        foreach (Transform child in this.transform)
        {
            child.Destroy();
        }

        this.IsBusy = false;
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);
    }

    /// <summary>
    /// Update the collection of active chunks in the world.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    private async Task UpdateChunks()
    {
        if (IsBusy)
            return;
        IsBusy = true;

        List<Vector3Int> visibleChunksCoordinates = Layout.GetActiveChunkCoordinates(this.Follower.position);

        List<Vector3Int> invalidChunks = new List<Vector3Int>();
        foreach (var key in this.ActiveChunks.Keys)
        {
            if (visibleChunksCoordinates.Contains(key))
            {
                invalidChunks.Add(key);
            }
        }

        foreach (var invalidKey in invalidChunks)
        {
            ActiveChunks.Remove(invalidKey);
        }

        foreach (var chunk in visibleChunksCoordinates)
        {
            ChunkController controller;

            if (!ActiveChunks.ContainsKey(chunk))
            {
                if (CacheChunks.ContainsKey(chunk))
                {
                    controller = CacheChunks[chunk];
                    this.ActiveChunks.Add(chunk, controller);
                }
                else
                {
                    controller = Factory.CreateChunkController(chunk, Configuration, this.transform);
                    this.ActiveChunks.Add(chunk, controller);

                    await controller.UpdateChunkAsync();
                }
            }
            else
            {
                controller = ActiveChunks[chunk];
            }

            if (controller == null)
                throw new System.ArgumentNullException("ChunkController does not exist. Was the gameObject deleted?");
        }
    }

    /// <summary>
    /// Returns whether the follower has walked far enough away from their last position that we should update the list of active chunks.
    /// </summary>
    /// <returns></returns>
    private bool IsFollowerOutsideOfRange()
    {
        float viewerDistance = Vector3.Distance(Follower.position, LastKnownFollowerPosition);
        if (viewerDistance > TravelDistanceToUpdateChunks)
        {
            LastKnownFollowerPosition = Follower.position;
            return true;
        }

        return false;
    }
}