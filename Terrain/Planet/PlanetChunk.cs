using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetChunk : MonoBehaviour
{
    public Vector3Int Coordinates;
    public Planet Planet;

    private PlanetChunkThreadData threadData;
    public int RenderDetail { get; set; } = -1;

    public async Task Generate(Planet owner, Vector3Int coordinates)
    {
        this.Planet = owner;
        this.Coordinates = coordinates;

        this.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        this.GetComponent<MeshRenderer>().material.SetFloat("_Smoothness", 0f);

        this.name = $"Chunk X:{coordinates.x} Y:{coordinates.y} Z:{coordinates.z}";
        await this.UpdateAsync(true);
    }

    public void ModifyMap(Vector3 hitPoint, float radius, float intensity, bool adding = true)
    {
        MarchingCubes.ModifyMapWithBrush(ref threadData.MapData.DensityMap, this.Coordinates, hitPoint, radius, intensity, adding);
        ScheduleUpdate();
    }

    private bool updateScheduled = false;
    private bool updateRequested = false;

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
            ScheduleUpdate(); // run the queued update
        }
    }



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

        Mesh newMesh = MarchingCubes.GenerateMesh(threadData.Cube);

        this.GetComponent<MeshFilter>().sharedMesh = newMesh;
        this.GetComponent<MeshRenderer>().material.mainTexture = TextureGenerator.TextureFromColourMap(threadData.ColorMap, Planet.Universe.PlanetChunkSize, Planet.Universe.PlanetChunkSize);
        this.GetComponent<MeshCollider>().sharedMesh = newMesh;
    }

    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

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