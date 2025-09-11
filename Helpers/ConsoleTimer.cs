using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public static class ConsoleTimer
{
    private static readonly Dictionary<string, Stopwatch> watches = new();

    public static void Start(string name)
    {
        if (!watches.TryGetValue(name, out var sw))
        {
            sw = new Stopwatch();
            watches[name] = sw;
        }
        else
        {
            sw.Reset();
        }

        sw.Start();
    }

    public static string Stop(string name)
    {
        if (!watches.TryGetValue(name, out var sw))
            return $"No stopwatch named {name}";

        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds;
        return $"{name} timer ran for {ms:F3} ms";
    }

    public static void WriteToConsole()
    {
        if (watches.Count == 0)
        {
            UnityEngine.Debug.Log("No timers running.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== ConsoleTimer Results ===");
        sb.AppendLine(string.Format("{0,-20} {1,10}", "Name", "Elapsed (ms)"));
        sb.AppendLine(new string('-', 32));

        foreach (var kvp in watches)
        {
            string name = kvp.Key;
            var sw = kvp.Value;
            double ms = sw.Elapsed.TotalMilliseconds;

            sb.AppendLine(string.Format("{0,-20} {1,10:F3}", name, ms));
        }

        UnityEngine.Debug.Log(sb.ToString());
    }
}
