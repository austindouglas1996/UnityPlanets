using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetGenerator))]
public class PlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default inspector UI

        PlanetGenerator generator = (PlanetGenerator)target;

        if (GUILayout.Button("Regenerate Cave"))
        {
            generator.Generate();
        }
    }
}