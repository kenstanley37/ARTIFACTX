namespace NMS.Tools.SaveInspector.Services;

/// <summary>
/// Single write-through point for all output: every line goes to both the console
/// and a log file next to the exe, so a session's findings survive after the
/// terminal closes. The log is cleared at the start of each run.
/// </summary>
public static class LogService
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "SaveInspector.log.txt");

    static LogService()
    {
        File.WriteAllText(LogPath, "");
    }

    public static void Write(string message)
    {
        File.AppendAllText(LogPath, message + Environment.NewLine);
        Console.WriteLine(message);
    }
}