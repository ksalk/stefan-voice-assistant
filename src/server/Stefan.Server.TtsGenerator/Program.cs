using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stefan.Server.Application;
using Stefan.Server.Application.Services;

var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

var text = cliArgs.FirstOrDefault(a => !a.StartsWith("-"))
    ?? throw new ArgumentException("Text argument is required. Usage: dotnet run -- \"Hello world\" [-o output.wav]");

var outputPath = ParseOutputPath(cliArgs);
var fileName = string.IsNullOrEmpty(outputPath) ? "output.wav" : outputPath;

var host = CreateHost();
var tts = host.Services.GetRequiredService<ITextToSpeechService>();

var result = await tts.SynthesizeAsync(text);

if (result.IsSuccess)
{
    Directory.CreateDirectory(Path.GetDirectoryName(fileName) ?? ".");
    File.WriteAllBytes(fileName, result.Value.AudioBytes);
    Console.WriteLine($"Generated: {fileName} ({result.Value.AudioBytes.Length:N0} bytes, {result.Value.DurationMs:F0}ms)");
}
else
{
    Console.Error.WriteLine($"Error: {result.Error}");
    Environment.Exit(1);
}

IHost CreateHost()
{
    var host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration((context, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory());
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile("appsettings.Development.json", optional: true);
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddApplication(context.Configuration);
        })
        .Build();

    return host;
}

string ParseOutputPath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "-o" or "--output")
            return args[i + 1];
    }
    return string.Empty;
}
