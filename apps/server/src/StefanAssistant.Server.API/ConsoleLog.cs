namespace StefanAssistant.Server.API;

public enum LogCategory { HTTP, STT, LLM, Tool }

public static class ConsoleLog
{
    private static readonly Dictionary<LogCategory, ConsoleColor> Colors = new()
    {
        [LogCategory.HTTP] = ConsoleColor.Blue,
        [LogCategory.STT]  = ConsoleColor.Cyan,
        [LogCategory.LLM]  = ConsoleColor.Magenta,
        [LogCategory.Tool] = ConsoleColor.Yellow,
    };

    public static void Write(LogCategory category, string message)
    {
        Console.ForegroundColor = Colors[category];
        Console.Write($"[{category}]");
        Console.ResetColor();
        Console.WriteLine($" {message}");
    }

    public static void WriteSeparator()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('*', 60));
        Console.ResetColor();
    }
}
