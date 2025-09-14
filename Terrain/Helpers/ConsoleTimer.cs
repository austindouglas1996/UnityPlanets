using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Quick Unity helper for timing code. 
/// Wraps <see cref="Stopwatch"/> so you can start/stop timers by name.
/// - Call <see cref="Start(string)"/> to begin a timer.
/// - Call <see cref="Stop(string)"/> to end it and get the result as text.
/// - Call <see cref="WriteToConsole"/> to dump all active timers to the Unity console.
/// </summary>
public static class ConsoleTimer
{
    private static readonly Dictionary<string, Stopwatch> watches = new();

    /// <summary>
    /// Start (or restart) a timer with the given name.
    /// </summary>
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

    /// <summary>
    /// Stop the timer with the given name and return a formatted result.
    /// </summary>
    public static string Stop(string name)
    {
        if (!watches.TryGetValue(name, out var sw))
            return $"No stopwatch named {name}";

        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds;
        return $"{name} timer ran for {ms:F3} ms";
    }

    /// <summary>
    /// Write all current timers and their elapsed times to the Unity console.
    /// </summary>
    public static void WriteToConsole()
    {
        if (watches.Count == 0)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ConsoleTimer Results");
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
