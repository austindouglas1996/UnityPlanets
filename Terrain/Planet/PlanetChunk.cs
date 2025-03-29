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

            threadData = new PlanetChunkThreadData(mapData, cubeProcesor);
        });

        if (threadData == null)
        {
            return;
        }

        this.GetComponent<MeshFilter>().sharedMesh = MeshGenerator.GenerateMarchingCubeMesh(threadData.Cube);

        if (GetComponent<MeshCollider>() != null)
        {
            Destroy(GetComponent<MeshCollider>());
        }

        this.gameObject.AddComponent<MeshCollider>();
    }

    public void SetVisible(bool visible)
    {
        this.gameObject.SetActive(visible);
    }

    public class PlanetChunkThreadData
    {
        public PlanetChunkThreadData(PlanetMapData mapData, MarchingCube cube)
        {
            this.MapData = mapData;
            this.Cube = cube;
        }

        public PlanetMapData MapData { get; set; }
        public MarchingCube Cube { get; set; }
    }
}