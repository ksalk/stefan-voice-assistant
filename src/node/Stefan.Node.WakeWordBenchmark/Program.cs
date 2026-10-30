using System.Text.Json;
using Microsoft.Extensions.Options;
using Stefan.Node.Audio;
using Stefan.Node.Options;

namespace Stefan.Node.WakeWordBenchmark;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = BenchmarkOptions.CreateFromArguments(args);
            new BenchmarkRunner(options).Run();
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
