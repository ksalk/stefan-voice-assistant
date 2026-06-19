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
                // Create the FIFO under a temporary name, fix its permissions, then atomically rename
                // it to the final path. The atomic rename ensures readers on the host never observe
                // the FIFO before chmod has granted write permission (umask 0022 would otherwise leave
                // it at 0644 between mkfifo and chmod, causing EACCES on the host's open() for writing).
                var tempPath = pipePath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                // Permissions need to be set in octal format, e.g. 0666 for read/write permissions for everyone
                var result = mkfifo(tempPath, Convert.ToUInt32("666", 8));
                if (result != 0)
                {
                    throw new IOException($"Failed to create named pipe at {pipePath}. Error code: {result}");
                }
                chmod(tempPath, Convert.ToUInt32("666", 8));
                File.Move(tempPath, pipePath);
                Console.WriteLine($"[pipe] Named pipe created: {pipePath}");
            }

            Console.WriteLine($"[pipe] Waiting for writer to open pipe: {pipePath}");

            await using var pipeStream = new FileStream(pipePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: ChunkSize, useAsync: true);

            Console.WriteLine("[pipe] Writer connected, reading audio data");

            var buffer = new byte[ChunkSize];
            int bytesRead;

            while ((bytesRead = await pipeStream.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken)) > 0)
            {
                var chunk = new RawPcmChunk
                {
                    Bytes = buffer[..bytesRead].ToArray(),
                    Format = new AudioFormat
                    {
                        Channels = input.Channels,
                        SampleRate = input.SampleRate,
                        BitsPerSample = input.BitsPerSample
                    }
                };
                var output = AudioProcessing.ConvertAndResample(chunk, input.ProcessingSampleRate);
                await audioWriter.WriteAsync(output.Bytes, cancellationToken);
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
