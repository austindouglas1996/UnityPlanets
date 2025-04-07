using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using VHierarchy.Libs;

public class ChunkManager : MonoBehaviour
{
    [HideInInspector] public Transform Follower;

    [Header("Rendering")]
    [Tooltip("How far a given chunk can be that it will be rendered on screen. Details will automatically be adjusted on distance.")]
    public float ChunkRenderDistance = 400;

    [Tooltip("How far the follower needs to be travel before we update the active chunks.")]
    public float TravelDistanceToUpdateChunks = 10f;

    [SerializeField] private IChunkConfiguration Configuration;
    [SerializeField] private IChunkLayout Layout;
    [SerializeField] private IChunkControllerFactory Factory;

    private Dictionary<Vector3Int, ChunkController> ActiveChunks = new Dictionary<Vector3Int, ChunkController>();
    private Dictionary<Vector3Int, ChunkController> CacheChunks = new Dictionary<Vector3Int, ChunkController>();

    private Vector3 LastKnownFollowerPosition;

    private bool IsBusy = false;
    private bool IsInitialized = false;

    private void Awake()
    {
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);
    }

    private async void Update()
    {
        if (IsFollowerOutsideOfRange())
        {
            UpdateChunks();
        }
    }

    public void Initialize(IChunkConfiguration configuration, IChunkLayout layout, IChunkControllerFactory factory)
    {
        if (configuration == null)
            throw new System.ArgumentNullException("Configuration is null.");
        if (layout == null)
            throw new System.ArgumentNullException("Layout is null.");
        if (factory == null)
            throw new System.ArgumentNullException("Factory is null.");

        this.Configuration = configuration;
        this.Layout = layout;
        this.Factory = factory;

        this.IsInitialized = true;
    }

    public void ReProcessColors()
    {
        foreach (Transform child in this.transform)
        {
            ChunkController controller = child.GetComponent<ChunkController>();
            if (controller != null)
            {
                controller.ApplyChunkColors();
            }
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
            Destroy(child.gameObject);
        }

        this.IsBusy = false;
        this.LastKnownFollowerPosition = new Vector3(999, 999, 999);
    }

    public async Task ModifyTerrain(TerrainBrush brush, bool isAdding, float bufferMultiplier = 0.5f, CancellationToken token = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        Bounds brushBounds = brush.GetBrushBounds();
        Vector3 chunkSize = new Vector3(Configuration.ChunkSize, Configuration.ChunkSize, Configuration.ChunkSize);

        foreach (ChunkController chunk in this.ActiveChunks.Values)
        {
            Bounds chunkBounds = new Bounds(chunk.transform.position + chunkSize * bufferMultiplier, chunkSize);

            if (brushBounds.Intersects(chunkBounds))
            {
                await chunk.ModifyChunk(brush, isAdding, token);
            }
        }

        sw.Stop();

        UnityEngine.Debug.Log($"ModifyChunks took {sw.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Update the collection of active chunks in the world.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    private void UpdateChunks()
    {
        if (IsBusy || !IsInitialized)
            return;
        IsBusy = true;

        List<Vector3Int> visibleChunksCoordinates = Layout.GetActiveChunkCoordinates(this.Follower.position);

        List<Vector3Int> invalidChunks = new List<Vector3Int>();
        foreach (var key in this.ActiveChunks.Keys)
        {
            if (!visibleChunksCoordinates.Contains(key))
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
                }
            }
            else
            {
                controller = ActiveChunks[chunk];
            }

            if (controller == null)
                throw new System.ArgumentNullException("ChunkController does not exist. Was the gameObject deleted?");
        }

        IsBusy = false;
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