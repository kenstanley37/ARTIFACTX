namespace ArtifactX.Tools.SaveInspector.Services;

public static class ConsoleStyle
{
    public static void Header(string text) => Write(text, ConsoleColor.Cyan);
    public static void Success(string text) => Write(text, ConsoleColor.Green);
    public static void Warning(string text) => Write(text, ConsoleColor.Yellow);
    public static void Error(string text) => Write(text, ConsoleColor.Red);
    public static void Info(string text) => Write(text, ConsoleColor.Gray);

    private static void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        LogService.Write(text);
        Console.ResetColor();
    }
}