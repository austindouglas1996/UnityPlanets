using UnityEngine;

public class PlanetChunk : MonoBehaviour
{
    public Vector3Int Coordinates = new Vector3Int(0, 0, 0);
    public Vector3 Position = new Vector3(0, 0, 0);
    public Vector3Int Size = new Vector3Int(0, 0, 0);
    private float[,,] DensityMap;
    private PlanetGenerator PlanetGenerator;

    public void Generate(PlanetGenerator generator, Vector3Int coordinates, int size)
    {
        this.PlanetGenerator = generator;
        this.Coordinates = coordinates;
        this.Position = new Vector3(coordinates.x * size, coordinates.y * size, coordinates.z * size);
        this.Size = new Vector3Int(size, size, size);

        this.name = $"Chunk X:{coordinates.x} Y:{coordinates.y} Z: {coordinates.z}";

        DensityMap = MarchingCubes.GenerateRoundMap(this.Size, coordinates, generator.WorldCenter, generator.Radius);

        this.GenerateTerrain();
    }

    public void UpdateMap(Vector3 hitPoint, float radius, float intensity, bool adding = true)
    {
        MarchingCubes.ModifyMapWithBrush(ref DensityMap, this.Coordinates, hitPoint, radius, intensity, adding);

        this.GenerateTerrain();
    }

    public void GenerateTerrain()
    {
        var cube = new MarchingCube();
        cube.Process(DensityMap, 0.5f, new Vector3(0, 0, 0));

        GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateMarchingCubeMesh(cube);

        GetComponent<FoliageGenerator>().ApplyMap(PlanetGenerator.Store, cube);

        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (GetComponent<MeshCollider>() != null)
        {
            Destroy(GetComponent<MeshCollider>());
        }

        this.gameObject.AddComponent<MeshCollider>();
    }
}