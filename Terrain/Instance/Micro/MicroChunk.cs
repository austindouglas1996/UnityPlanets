using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class MicroChunk : MonoBehaviour
{
    [SerializeField] private ChunkController Controller;
    [SerializeField] private IChunkConfiguration Configuration;
    [SerializeField] private IChunkControllerFactory Factory;

    [SerializeField] private ChunkController Owner;
    [SerializeField] private Vector3 LocalPosition;
    [SerializeField] private List<Vector3> Points;
    [SerializeField] private Vector3 LocalSize;

    private bool IsInitialized = false;
    private bool Generated = false;

    private void Update()
    {
        if (!Generated && Owner.RenderedOnce)
        {
            Generated = true;
            Init();
        }

        if (this.Controller.RenderedOnce)
        {
            this.Controller.ApplyChunkColors();
        }
    }

    private void Init()
    {
        this.Controller.Initialize(new MicroChunkGenerator(Owner, Points, LocalSize), new MicroChunkColorizer(), Configuration, Owner.Coordinates);
        this.Controller.name = $"MICRO {Owner.Coordinates}";
    }

    public void Initialize(ChunkController owner, List<Vector3> points, Vector3 localPosition, Vector3 localSize, IChunkConfiguration configuration, IChunkControllerFactory factory)
    {
        if (configuration == null)
            throw new System.ArgumentNullException("Configuration is null.");

        this.Configuration = configuration;
        this.Factory = factory;
        this.Points = points;

        this.Owner = owner;
        this.LocalPosition = localPosition;
        this.LocalSize = localSize;

        // Create the micro mesh?
        this.Controller = Factory.CreateChunkController(Owner.Coordinates, Configuration, Owner.transform);

        this.IsInitialized = true;
    }
}