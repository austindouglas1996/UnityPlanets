using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetChunk : MonoBehaviour
{
    public Vector3Int Coordinates;
    public Planet Planet;

    private PlanetChunkThreadData threadData;
    public int RenderDetail { get; set; } = -1;

    private bool updateScheduled = false;
    private bool updateRequested = false;

    private void Start()
    {
        this.AddComponent<FoliageGenerator>();
    }

    /// <summary>
    /// Start the initial generation of the planet.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="coordinates"></param>
    /// <returns></returns>
    public async Task Generate(Planet owner, Vector3Int coordinates)
    {
        this.Planet = owner;
        this.Coordinates = coordinates;

        this.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        this.GetComponent<MeshRenderer>().material.SetFloat("_Smoothness", 0f);

        this.name = $"Chunk X:{coordinates.x} Y:{coordinates.y} Z:{coordinates.z}";
        await this.UpdateAsync(true);
    }

    /// <summary>
    /// Modify the terrain map using a brush. Basic modification.
    /// </summary>
    /// <param name="hitPoint"></param>
    /// <param name="radius"></param>
    /// <param name="intensity"></param>
    /// <param name="adding"></param>
    public void ModifyMap(Vector3 hitPoint, float radius, float intensity, bool adding = true)
    {
        MarchingCubes.ModifyMapWithBrush(ref threadData.MapData.DensityMap, this.Coordinates, hitPoint, radius, intensity, adding);
        ScheduleUpdate();
    }

    /// <summary>
    /// Schedule an update for the chunk to be updated. This helps with modifying the terrain but not wanting an extra every single tiny edit.
    /// </summary>
    private async void ScheduleUpdate()
    {
        if (updateScheduled)
        {
            updateRequested = true;
            return;
        }

        updateScheduled = true;

        await Task.Delay(50); // debounce window
        await UpdateAsync();

        updateScheduled = false;

        if (updateRequested)
        {
            updateRequested = false;
            ScheduleUpdate();
        }
    }

    /// <summary>
    /// Update the chunk data. Processing the density map, and recreating the mesh and its colors.
    /// </summary>
    /// <param name="initial"></param>
    /// <returns></returns>
    public async Task UpdateAsync(bool initial = false)
    {
        await Task.Run(() =>
        {
            PlanetMapData mapData;

            if (initial || threadData == null)
                mapData = this.Planet.GenerateMap(this.Coordinates);
            else
                mapData = threadData.MapData;

            MarchingCube cubeProcesor = new MarchingCube();
            cubeProcesor.Process(mapData.DensityMap, this.Planet.Threshold, new Vector3(0, 0, 0));

            threadData = new PlanetChunkThreadData(mapData, mapData.ColorMap, cubeProcesor);
        });

        if (threadData == null)
        {
            return;
        }

        //this.GetComponent<FoliageGenerator>().ApplyMap(Planet.Universe.Store, threadData.Cube);

        Mesh newMesh = MarchingCubes.GenerateMesh(threadData.Cube);

        this.GetComponent<MeshFilter>().sharedMesh = newMesh;
        this.GetComponent<MeshRenderer>().material.mainTexture = TextureGenerator.TextureFromColourMap(threadData.ColorMap, Planet.Universe.PlanetChunkSize, Planet.Universe.PlanetChunkSize);
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;
    }

    /// <summary>
    /// Set the chunk visible. Helps with knowing whether to continue to render/update content.
    /// </summary>
    /// <param name="visible"></param>
    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Container for transferring data through threads.
    /// </summary>
    public class PlanetChunkThreadData
    {
        public PlanetChunkThreadData(PlanetMapData mapData, Color[] color, MarchingCube cube)
        {
            this.MapData = mapData;
            this.ColorMap = color;
            this.Cube = cube;
        }

        public PlanetMapData MapData { get; set; }
        public Color[] ColorMap { get; set; }
        public MarchingCube Cube { get; set; }
    }
}