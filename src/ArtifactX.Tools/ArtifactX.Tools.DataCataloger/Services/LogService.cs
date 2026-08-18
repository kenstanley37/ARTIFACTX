namespace ArtifactX.Tools.DataCataloger.Services;

public static class LogService
{
    private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "DataCataloger.log.txt");

    static LogService()
    {
        // Clear log file on startup
        File.WriteAllText(LogPath, "");
    }

    public static void Write(string message)
    {
        File.AppendAllText(LogPath, message + Environment.NewLine);
        Console.WriteLine(message);
    }

    public static void WriteRaw(byte[] bytes)
    {
        File.AppendAllText(LogPath, BitConverter.ToString(bytes) + Environment.NewLine);
        Console.WriteLine(BitConverter.ToString(bytes));
    }
}
