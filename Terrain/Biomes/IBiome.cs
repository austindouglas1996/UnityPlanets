public interface IBiome
{
    float Modify(float baseVal, Vector3 worldPos);
    Color GetColor();
}