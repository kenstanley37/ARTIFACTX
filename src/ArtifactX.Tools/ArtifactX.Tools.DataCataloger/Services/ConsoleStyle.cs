namespace ArtifactX.Tools.DataCataloger.Services;

public static class ConsoleStyle
{
    public static void Header(string text)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        LogService.Write(text);
        Console.ResetColor();
    }

    public static void Success(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        LogService.Write(text);
        Console.ResetColor();
    }

    public static void Warning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        LogService.Write(text);
        Console.ResetColor();
    }

    public static void Error(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        LogService.Write(text);
        Console.ResetColor();
    }

    public static void Info(string text)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        LogService.Write(text);
        Console.ResetColor();
    }
}