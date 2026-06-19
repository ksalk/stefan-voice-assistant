using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

internal sealed record AudioPlaybackItem(string FilePath, bool IsTemp);

public class AudioPlayer : BackgroundService
{
    private readonly Channel<AudioPlaybackItem> _queue;
    private readonly ILogger<AudioPlayer> _logger;
    private readonly string _outputDeviceName;
    private readonly string _volumeControlName;
    private volatile CancellationTokenSource? _currentCts;

    public AudioPlayer(ILogger<AudioPlayer> logger, IOptions<AudioOptions> audioOptions)
    {
        _logger = logger;
        _outputDeviceName = audioOptions.Value.Output.DeviceName;
        _volumeControlName = audioOptions.Value.Output.VolumeControlName;
        _queue = Channel.CreateUnbounded<AudioPlaybackItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public void Queue(byte[] wavBytes)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"stefan_audio_{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(tempPath, wavBytes);
        _logger.LogDebug("[audio] Queued audio from bytes -> {TempPath}", tempPath);
        _queue.Writer.TryWrite(new AudioPlaybackItem(tempPath, IsTemp: true));
    }

    public void Queue(string filePath)
    {
        _logger.LogDebug("[audio] Queued audio file: {FilePath}", filePath);
        _queue.Writer.TryWrite(new AudioPlaybackItem(filePath, IsTemp: false));
    }

    /// <summary>Plays WAV bytes directly, bypassing the queue. Awaits completion.</summary>
    public async Task PlayAsync(byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"stefan_audio_{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(tempPath, wavBytes);
        try
        {
            await PlayFileAsync(tempPath, cancellationToken);
        }
        finally
        {
            try { /* File.Delete(tempPath); */ } catch { }
        }
    }

    public void CancelCurrent()
    {
        var cts = _currentCts;
        if (cts is not null)
        {
            _logger.LogInformation("[audio] Cancelling current audio playback.");
            cts.Cancel();
        }
        else
        {
            _logger.LogInformation("[audio] No current audio playback to cancel.");
        }
    }

    public int? Volume
    {
        get => TryGetVolume();
        set
        {
            if (value.HasValue)
                TrySetVolume(value.Value);
        }
    }

    private int? TryGetVolume()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "amixer",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.StartInfo.ArgumentList.Add("-D");
            process.StartInfo.ArgumentList.Add(_outputDeviceName);
            process.StartInfo.ArgumentList.Add("sget");
            process.StartInfo.ArgumentList.Add(_volumeControlName);

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("[audio] amixer sget exited with {ExitCode}", process.ExitCode);
                return null;
            }

            return ParseVolumePercent(output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[audio] Failed to read volume from amixer.");
            return null;
        }
    }

    private bool TrySetVolume(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "amixer",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.StartInfo.ArgumentList.Add("-D");
            process.StartInfo.ArgumentList.Add(_outputDeviceName);
            process.StartInfo.ArgumentList.Add("sset");
            process.StartInfo.ArgumentList.Add(_volumeControlName);
            process.StartInfo.ArgumentList.Add($"{clamped}%");

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("[audio] amixer sset exited with {ExitCode}", process.ExitCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[audio] Failed to set volume via amixer.");
            return false;
        }
    }

    private static int? ParseVolumePercent(string output)
    {
        var match = Regex.Match(output, @"\[(\d+)%\]");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var pct))
            return null;

        return Math.Clamp(pct, 0, 100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[audio] AudioPlayer started.");

        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var itemCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _currentCts = itemCts;

            try
            {
                _logger.LogInformation("[audio] Playing audio: {FilePath}", item.FilePath);
                await PlayFileAsync(item.FilePath, itemCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Individual item was cancelled via CancelCurrent() — continue to next item
                _logger.LogInformation("[audio] Audio playback cancelled, continuing queue.");
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down — rethrow so BackgroundService can handle it
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[audio] Error during audio playback of {FilePath}", item.FilePath);
            }
            finally
            {
                _currentCts = null;

                if (item.IsTemp)
                {
                    try
                    {
                        //File.Delete(item.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[audio] Failed to delete temp audio file: {FilePath}", item.FilePath);
                    }
                }
            }
        }
    }

    private async Task PlayFileAsync(string filePath, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "aplay",
                ArgumentList = { "-D", _outputDeviceName, filePath },
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            }
        };

        process.Start();

        try
        {
            var durationMs = TryGetWavDurationMs(filePath);
            if (durationMs is > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(durationMs.Value), cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Process may have already exited
            }

            throw;
        }
    }

    private static long? TryGetWavDurationMs(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs);
            if (fs.Length < 44)
                return null;

            if (!ReadTag(br, "RIFF"))
                return null;
            br.ReadUInt32();
            if (!ReadTag(br, "WAVE"))
                return null;

            int sampleRate = 0, channels = 0, bitsPerSample = 0;
            long dataOffset = -1;
            long dataSize = 0;

            while (fs.Position + 8 <= fs.Length)
            {
                var chunkId = br.ReadBytes(4);
                var chunkSize = br.ReadInt32();

                if (chunkId[0] == 'f' && chunkId[1] == 'm' && chunkId[2] == 't' && chunkId[3] == ' ')
                {
                    br.ReadUInt16();
                    channels = br.ReadUInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32();
                    br.ReadUInt16();
                    bitsPerSample = br.ReadUInt16();
                    fs.Seek(chunkSize - 16, SeekOrigin.Current);
                    if (chunkSize % 2 == 1)
                        fs.Seek(1, SeekOrigin.Current);
                }
                else if (chunkId[0] == 'd' && chunkId[1] == 'a' && chunkId[2] == 't' && chunkId[3] == 'a')
                {
                    dataOffset = fs.Position;
                    dataSize = chunkSize;
                    break;
                }
                else
                {
                    fs.Seek(chunkSize, SeekOrigin.Current);
                    if (chunkSize % 2 == 1)
                        fs.Seek(1, SeekOrigin.Current);
                }
            }

            if (dataOffset < 0 || sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0)
                return null;

            if (dataSize <= 0 || dataOffset + dataSize > fs.Length)
                dataSize = fs.Length - dataOffset;

            var byteRate = sampleRate * channels * (bitsPerSample / 8);
            if (byteRate <= 0)
                return null;

            return (long)(dataSize * 1000.0 / byteRate);
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadTag(BinaryReader br, string expected)
    {
        if (br.BaseStream.Position + 4 > br.BaseStream.Length)
            return false;
        var bytes = br.ReadBytes(4);
        for (var i = 0; i < 4; i++)
        {
            if (bytes[i] != expected[i])
                return false;
        }
        return true;
    }
}
