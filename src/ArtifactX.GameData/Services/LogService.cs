namespace ArtifactX.GameData.Services;

/// <summary>
/// Where the catalog builder's messages go. The DataCataloger tool keeps the
/// old behaviour (console + DataCataloger.log.txt next to the exe); the app
/// points the log at its own folder, turns the console off and listens to
/// <see cref="MessageWritten"/> for progress (2026-10-14).
///
/// The file stays open between writes (it used to be opened and closed for
/// every line - thousands per build).
/// </summary>
public static class LogService
{
    private static readonly object Lock = new();
    private static string? _logPath = Path.Combine(AppContext.BaseDirectory, "DataCataloger.log.txt");
    private static StreamWriter? _writer;

    /// <summary>Every message, as it's written (on the builder's thread).</summary>
    public static event Action<string>? MessageWritten;

    public static bool EchoToConsole { get; set; } = true;

    /// <summary>Sends the log to this file (emptied on the next write), or nowhere with null.</summary>
    public static void UseLogFile(string? path)
    {
        lock (Lock)
        {
            Close();
            _logPath = path;
        }
    }

    /// <summary>Flushes and closes the log file (the next write starts it afresh).</summary>
    public static void Close()
    {
        lock (Lock)
        {
            _writer?.Dispose();
            _writer = null;
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
                    if (_writer is null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                        _writer = new StreamWriter(new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                    }
                    _writer.WriteLine(message);
                }
                catch (IOException) { _logPath = null; }
                catch (UnauthorizedAccessException) { _logPath = null; }
            }
        }
        if (EchoToConsole) Console.WriteLine(message);
        MessageWritten?.Invoke(message);
    }

    public static void WriteRaw(byte[] bytes) => Write(BitConverter.ToString(bytes));
}
