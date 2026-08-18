namespace ArtifactX.Tools.DataCataloger.Services;

public static class Logger
{
    private static readonly ConsoleColor[] FileColors = {
        ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.Yellow,
        ConsoleColor.Green, ConsoleColor.DarkCyan, ConsoleColor.White
    };

    private static int _colorIndex = 0;
    private static ConsoleColor _currentColor = ConsoleColor.White;

    public static void SetFileContext()
    {
        _currentColor = FileColors[_colorIndex % FileColors.Length];
        _colorIndex++;
    }

    public static void Log(string message)
    {
        Console.ForegroundColor = _currentColor;
        LogService.Write($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ResetColor();
    }

    public static void LogSystem(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        LogService.Write($"[SYSTEM] {message}");
        Console.ResetColor();
    }

    // New LogError method with Red coloring
    public static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        LogService.Write($"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}");
        Console.ResetColor();
    }
}