namespace StefanAssistant.Server.Tools.Timer;

public class TimerTools
{
    public static string AddTimer(int seconds, string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            Console.WriteLine($"Adding timer for {seconds} seconds");
        
            return $"Timer set for {seconds} seconds.";
        }
        
        Console.WriteLine($"Adding timer: {label} for {seconds} seconds");
        
        return $"Timer '{label}' set for {seconds} seconds.";
    }
}