namespace ArtifactX.GameData.Services;

/// <summary>
/// Where the catalog builder's messages go. The DataCataloger tool keeps the
/// old behaviour (console + DataCataloger.log.txt next to the exe); the app
/// points the log at its own folder, turns the console off and listens to
/// <see cref="MessageWritten"/> for progress (2026-10-14).
/// </summary>
public static class LogService
{
    private static readonly object Lock = new();
    private static string? _logPath = Path.Combine(AppContext.BaseDirectory, "DataCataloger.log.txt");
    private static bool _started;

    /// <summary>Every message, as it's written (on the builder's thread).</summary>
    public static event Action<string>? MessageWritten;

    public static bool EchoToConsole { get; set; } = true;

    /// <summary>Sends the log to this file (emptied on the next write), or nowhere with null.</summary>
    public static void UseLogFile(string? path)
    {
        lock (Lock)
        {
            _logPath = path;
            _started = false;
        }
    }

    public static void Write(string message)
    {
        lock (Lock)
        {
            if (_logPath is not null)
            {
                try
                {
                    if (!_started)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                        File.WriteAllText(_logPath, "");
                        _started = true;
                    }
                    File.AppendAllText(_logPath, message + Environment.NewLine);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        if (EchoToConsole) Console.WriteLine(message);
        MessageWritten?.Invoke(message);
    }

    public static void WriteRaw(byte[] bytes) => Write(BitConverter.ToString(bytes));
}
