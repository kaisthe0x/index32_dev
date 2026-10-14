using Godot;

namespace MyGame;

/// <summary>
/// DEBUG: reads the compile-time log the build writes (<c>build_times.log</c> — see <c>mygamedev.csproj</c>). The
/// editor shows build messages only in its MSBuild panel, so the game prints the last compile itself when it is
/// started from the editor. Does nothing in an exported game.
/// </summary>
public static class BuildLog
{
    private const string LogPath = "res://build_times.log";

    /// <summary>Print the most recent compile (when, configuration, seconds, source files) to the Output panel.</summary>
    public static void PrintLastCompile()
    {
        if (!OS.HasFeature("editor"))
            return;
        string path = ProjectSettings.GlobalizePath(LogPath);
        if (!System.IO.File.Exists(path))
            return;
        string? last = null;
        foreach (string line in System.IO.File.ReadLines(path))
            if (line.Length > 0)
                last = line;
        if (last != null)
            GD.Print($"Last C# compile: {last}");
    }
}
