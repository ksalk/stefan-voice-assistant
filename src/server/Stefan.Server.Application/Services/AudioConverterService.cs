using System.Diagnostics;
using Stefan.Server.Common;

namespace Stefan.Server.Application.Services;

public class AudioConverterService
{
    private readonly string _ffmpegPath;

    public AudioConverterService()
    {
        _ffmpegPath = FindExecutable("ffmpeg")
            ?? throw new FileNotFoundException(
                "ffmpeg not found. Install ffmpeg and ensure it is on PATH.");
    }

    public async Task<byte[]> CompressToOpusAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        return await RunFfmpegAsync(wavBytes, ["-c:a", "libopus", "-b:a", "16k", "-f", "ogv"], cancellationToken);
    }

    public async Task<byte[]> DecompressFromOpusAsync(byte[] opusBytes, CancellationToken cancellationToken = default)
    {
        return await RunFfmpegAsync(opusBytes, ["-c:a", "pcm_s16le", "-f", "wav", "-ar", "22050", "-ac", "1"], cancellationToken);
    }

    private async Task<byte[]> RunFfmpegAsync(byte[] inputBytes, string[] outputArgs, CancellationToken cancellationToken)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-i", "pipe:0" };
        args.AddRange(outputArgs);
        args.Add("pipe:1");

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = string.Join(" ", args),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();

        using var ms = new MemoryStream();

        // Drain stdout and stderr while writing stdin. Waiting for the write to
        // finish first can deadlock when either pipe buffer fills up.
        var writeInputTask = WriteInputAsync(process, inputBytes, cancellationToken);
        var readOutputTask = process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        var readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await Task.WhenAll(writeInputTask, readOutputTask, readErrorTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            KillProcess(process);
            throw;
        }

        var stderr = await readErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {stderr.Trim()}");
        }

        return ms.ToArray();
    }

    private static async Task WriteInputAsync(Process process, byte[] inputBytes, CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(inputBytes, cancellationToken);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited before it could be killed.
        }
    }

    private static string? FindExecutable(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar == null) return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, name);
            if (File.Exists(fullPath)) return fullPath;

            var withExt = fullPath + ".exe";
            if (File.Exists(withExt)) return withExt;
        }

        return null;
    }
}
