using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

public class PipeAudioInputProvider(IOptions<AudioOptions> audioOptions) : IAudioInputProvider
{
    private const int ChunkSize = 4096;

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string pathname, uint mode);

    public async Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken)
    {
        var pipePath = audioOptions.Value.PipePath ?? "/tmp/audio-input";
        var input = audioOptions.Value.Input;

        Console.WriteLine($"[pipe] Setting up named pipe: {pipePath}");

        try
        {
            if (!File.Exists(pipePath))
            {
                // Permissions need to be set in octal format, e.g. 0666 for read/write permissions for everyone
                var result = mkfifo(pipePath, Convert.ToUInt32("666", 8));
                if (result != 0)
                {
                    throw new IOException($"Failed to create named pipe at {pipePath}. Error code: {result}");
                }
                chmod(pipePath, Convert.ToUInt32("666", 8));
                Console.WriteLine($"[pipe] Named pipe created: {pipePath}");
            }

            Console.WriteLine($"[pipe] Waiting for writer to open pipe: {pipePath}");

            await using var pipeStream = new FileStream(pipePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: ChunkSize, useAsync: true);

            Console.WriteLine("[pipe] Writer connected, reading audio data");

            var buffer = new byte[ChunkSize];
            int bytesRead;

            while ((bytesRead = await pipeStream.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken)) > 0)
            {
                var chunk = buffer.AsSpan(0, bytesRead);
                var outputBytes = AudioProcessing.ConvertAndResample(chunk, input.Channels, input.SampleRate, input.ProcessingSampleRate);
                await audioWriter.WriteAsync(outputBytes, cancellationToken);
            }

            Console.WriteLine("[pipe] Pipe closed by sender");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[pipe] Reading cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[pipe] Error reading from pipe: {ex}");
        }
        finally
        {
            audioWriter.Complete();
        }
    }
}
