using System.Diagnostics;
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
    private volatile CancellationTokenSource? _currentCts;

    public AudioPlayer(ILogger<AudioPlayer> logger, IOptions<AudioOptions> audioOptions)
    {
        _logger = logger;
        _outputDeviceName = audioOptions.Value.Output.DeviceName;
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
}
