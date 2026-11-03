using System.Threading.Channels;
using Alsa.Net;
using Microsoft.Extensions.Options;
using SherpaOnnx;
using Stefan.Node;
using Stefan.Node.Audio;
using Stefan.Node.Options;
using Stefan.Node.Services;

public class VoiceCommandDispatcher(
    RemoteServerClient remoteServerClient,
    IAudioInputProvider audioInputProvider,
    AppStateService appStateService,
    AudioPlayer audioPlayer,
    AudioRecorder audioRecorder,
    IWakeWordDetector wakeWordDetector,
    IOptions<KeywordSpotterOptions> keywordSpotterOptions,
    IOptions<AudioOptions> audioOptions,
    ILogger<VoiceCommandDispatcher> logger) : BackgroundService
{
    private readonly List<RawPcmChunk> _commandAudioBuffer = [];
    // The detector returns only a keyword, so this rolling pre-roll is the only way to keep
    // the audio that triggered it.
    private readonly List<RawPcmChunk> _wakeWordAudioBuffer = [];
    private double _wakeWordAudioDurationMs;
    private double _silentDurationMs;
    private DateTime _recordingStartTime = DateTime.MinValue;
    private Guid _currentCommandId;

    /// <summary>Log scope carrying the id of the command currently being processed.</summary>
    private Dictionary<string, object> CommandScope() => new() { [Correlation.CommandIdProperty] = _currentCommandId };

    private AudioFormat ProcessingAudioFormat => new AudioFormat
    {
        Channels = 1,
        SampleRate = audioOptions.Value.Input.ProcessingSampleRate,
        BitsPerSample = 16
    };

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
        try
        {
            var audioChannel = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true
                });

            var audioProcessingTask = RunAudioProcessingAsync(
                audioChannel.Reader,
                keyword =>
                {
                    using var scope = logger.BeginScope(CommandScope());
                    logger.LogInformation("[listener] Keyword detected: {Keyword} \nStarting command recording...", keyword);
                },
                cancellationToken);

            var recordMicTask = Task.Factory.StartNew(
                () => audioInputProvider.WriteAudioInput(audioChannel.Writer, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            logger.LogInformation("[listener] VoiceCommandDispatcher started. Listening for wake word...");

            await Task.WhenAll(recordMicTask, audioProcessingTask);
        }
        catch (OperationCanceledException)
        {
            throw; // Let BackgroundService handle graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[listener] Fatal error in VoiceCommandDispatcher");
            throw;
        }
    }

    private async Task RunAudioProcessingAsync(
        ChannelReader<byte[]> reader,
        Action<string> onKeywordDetected,
        CancellationToken cancellationToken)
    {
        using var stopKeywordSpotter = KeywordSpotterFactory.Create(
            keywordSpotterOptions.Value,
            keywordSpotterOptions.Value.StopKeywordsFile,
            audioOptions.Value.Input.ProcessingSampleRate);
        using var stopKeywordStream = stopKeywordSpotter.CreateStream();

        try
        {
            await foreach (var audioBytes in reader.ReadAllAsync(cancellationToken))
            {
                var chunk = new RawPcmChunk { Bytes = audioBytes, Format = ProcessingAudioFormat };
                audioRecorder.WriteProcessed(chunk);

                if (appStateService.CurrentState == VoiceAssistantState.ListeningForWakeWord)
                {
                    AppendWakeWordAudio(chunk);
                    ProcessWakeWord(chunk, onKeywordDetected);
                }

                if (appStateService.CurrentState == VoiceAssistantState.RecordingCommand)
                {
                    await ProcessRecordingAsync(chunk, stopKeywordSpotter, stopKeywordStream);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (ChannelClosedException)
        {
            // Expected when ALSA loop completes
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[listener] Error in inference worker");
        }
    }

    private void ProcessWakeWord(
        RawPcmChunk chunk,
        Action<string> onKeywordDetected)
    {
        try
        {
            var keyword = wakeWordDetector.Process(chunk);
            if (keyword is null)
            {
                return;
            }

            appStateService.CurrentState = VoiceAssistantState.RecordingCommand;
            _commandAudioBuffer.Clear();
            _silentDurationMs = 0f;
            _recordingStartTime = DateTime.UtcNow;
            _currentCommandId = Guid.NewGuid();

            audioPlayer.Queue(Path.Combine(AppContext.BaseDirectory, "Assets", "notification_sound.wav"));
            onKeywordDetected(keyword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[listener] Error during keyword inference");
        }
    }

    private async Task ProcessRecordingAsync(
        RawPcmChunk chunk,
        KeywordSpotter stopKeywordSpotter,
        OnlineStream stopKeywordStream)
    {
        _commandAudioBuffer.Add(chunk);

        var chunkDurationMs = GetDurationMs(chunk);
        var rms = AudioProcessing.ComputeRms(chunk);

        if (rms < audioOptions.Value.SilenceThreshold)
        {
            _silentDurationMs += chunkDurationMs;
        }
        else
        {
            _silentDurationMs = 0f;
        }

        var elapsedMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;

        // Check for stop keyword
        var monoSamples = AudioProcessing.ConvertPcm16ToFloat(chunk);
        if (monoSamples.Length == 0)
        {
            return;
        }

        stopKeywordStream.AcceptWaveform(chunk.Format.SampleRate, monoSamples);

        while (stopKeywordSpotter.IsReady(stopKeywordStream))
        {
            stopKeywordSpotter.Decode(stopKeywordStream);

            var keywordResult = stopKeywordSpotter.GetResult(stopKeywordStream);
            if (!string.IsNullOrWhiteSpace(keywordResult.Keyword))
            {
                using (logger.BeginScope(CommandScope()))
                {
                    logger.LogInformation("[listener] Stop keyword detected. Returning to wake word detection.");
                }

                ClearCommandBuffers();
                audioPlayer.CancelCurrent();

                appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
                stopKeywordSpotter.Reset(stopKeywordStream);
                return;
            }
        }

        if (_silentDurationMs >= audioOptions.Value.SilenceTimeoutMs || elapsedMs >= audioOptions.Value.MaxRecordingMs)
        {
            using var commandScope = logger.BeginScope(CommandScope());

            await SendCommandToServerAsync(_commandAudioBuffer, _wakeWordAudioBuffer);
            ClearCommandBuffers();
            appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
            logger.LogInformation("[listener] Finished command processing. Returning to wake word detection.");
        }
    }

    private void AppendWakeWordAudio(RawPcmChunk chunk)
    {
        var maxDurationMs = audioOptions.Value.WakeWordAudioMs;
        if (maxDurationMs <= 0)
        {
            return;
        }

        _wakeWordAudioBuffer.Add(chunk);
        _wakeWordAudioDurationMs += GetDurationMs(chunk);

        // Keep one chunk even when it is longer than the configured window.
        while (_wakeWordAudioDurationMs > maxDurationMs && _wakeWordAudioBuffer.Count > 1)
        {
            var removed = _wakeWordAudioBuffer[0];
            _wakeWordAudioBuffer.RemoveAt(0);
            _wakeWordAudioDurationMs -= GetDurationMs(removed);
        }
    }

    private void ClearCommandBuffers()
    {
        _commandAudioBuffer.Clear();
        _wakeWordAudioBuffer.Clear();
        _wakeWordAudioDurationMs = 0;
        _silentDurationMs = 0;
    }

    private static double GetDurationMs(RawPcmChunk chunk)
    {
        var bytesPerSecond = chunk.Format.SampleRate * chunk.Format.Channels * (chunk.Format.BitsPerSample / 8);
        return bytesPerSecond > 0 ? chunk.Bytes.Length * 1000.0 / bytesPerSecond : 0;
    }

    private async Task SaveRecordingAsync(List<RawPcmChunk> monoChunks)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, audioOptions.Value.Recording.Directory);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"command_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        var wavBytes = AudioProcessing.BuildWavBytes(monoChunks);
        await File.WriteAllBytesAsync(filePath, wavBytes);
        logger.LogInformation("[listener] Saved: {FilePath}", filePath);
    }

    private async Task SendCommandToServerAsync(List<RawPcmChunk> monoChunks, List<RawPcmChunk> wakeWordChunks)
    {
        byte[] wavBytes;
        try
        {
            logger.LogInformation("[listener] Sending command audio to server...");
            wavBytes = AudioProcessing.BuildWavBytes(monoChunks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[listener] Error building WAV data");
            return;
        }

        byte[]? wakeWordWavBytes = null;
        if (wakeWordChunks.Count > 0)
        {
            try
            {
                wakeWordWavBytes = AudioProcessing.BuildWavBytes(wakeWordChunks);
                logger.LogInformation("[listener] Wake word audio captured: {WakeWordBytes} bytes", wakeWordWavBytes.Length);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[listener] Error building wake word WAV data; sending the command without it");
            }
        }

        var result = await remoteServerClient.SendCommandAsync(wavBytes, _currentCommandId, wakeWordWavBytes);
        if (result.IsSuccess)
        {
            logger.LogInformation("[listener] Command sent successfully. Queuing response audio for playback.");
            audioPlayer.Queue(result.Value.Audio);
        }
        else
        {
            logger.LogWarning("[listener] Failed to send command: {Error}", result.Error);
            audioPlayer.Queue(Path.Combine(AppContext.BaseDirectory, "Assets", "command_failed.wav"));
        }
    }
}
