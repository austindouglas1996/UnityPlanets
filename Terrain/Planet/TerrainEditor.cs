using UnityEngine;

public class TerrainEditor : MonoBehaviour
{
    public float brushRadius = 5f;
    public float brushIntensity = 0.1f;
    public float isolevel = 0.5f;
    public PlanetGenerator generator;

    void Update()
    {
        if (Input.GetMouseButton(0)) // Left click to add terrain
        {
            TryModifyTerrain(true);
        }
        else if (Input.GetMouseButton(1)) // Right click to remove terrain
        {
            TryModifyTerrain(false);
        }
    }

    private void TryModifyTerrain(bool adding)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("Default");

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            Vector3 worldPos = hit.point;
            float brushRadius = this.brushRadius;

            Bounds brushBounds = new Bounds(worldPos, Vector3.one * brushRadius * 2);

            foreach (var pair in generator.Chunks)
            {
                PlanetChunk chunk = pair.Value;

                // Each chunk's world bounds
                Bounds chunkBounds = new Bounds(
                    chunk.transform.position + new Vector3(generator.ChunkSize, generator.ChunkSize, generator.ChunkSize) * 0.5f,
                    new Vector3(generator.ChunkSize, generator.ChunkSize, generator.ChunkSize)
                );

                if (brushBounds.Intersects(chunkBounds))
                {
                    // Modify this chunk using world-space brush
                    chunk.UpdateMap(worldPos, brushRadius, brushIntensity, adding);
                }
            }

        }
    }
}