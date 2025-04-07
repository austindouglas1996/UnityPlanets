using System;
using System.Threading.Tasks;
using UnityEngine;


public class TerrainEditor : MonoBehaviour
{
    [SerializeField] public BrushType SelectedBrush;
    public ChunkManager chunkManager;

    [SerializeField] public float modifyCooldown = 0.2f; // 200 ms
    private float lastModifyTime = 0f;

    async void Update()
    {
        bool isAdding = Input.GetMouseButton(0);
        bool isRemoving = Input.GetMouseButton(1);

        if ((isAdding || isRemoving) && Time.time - lastModifyTime > modifyCooldown)
        {
            lastModifyTime = Time.time;
            await TryModifyTerrain(isAdding);
        }
    }

    private TerrainBrush CreateBrush(Vector3 worldPos)
    {
        if (this.SelectedBrush == BrushType.Round)
        {
            return new RoundTerrainBrush(worldPos);
        }

        throw new System.NotSupportedException("Does not support that brush type.");
    }


    private async Task TryModifyTerrain(bool adding)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("Default");

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            await chunkManager.ModifyTerrain(CreateBrush(hit.point), adding);
        }
    }
}