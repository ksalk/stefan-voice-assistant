using System.Threading.Channels;
using Alsa.Net;
using Microsoft.Extensions.Options;
using SherpaOnnx;
using Stefan.Node.Audio;
using Stefan.Node.Options;
using Stefan.Node.Services;

public class VoiceCommandDispatcher(
    RemoteServerClient remoteServerClient,
    IAudioInputProvider audioInputProvider,
    AppStateService appStateService,
    AudioPlayer audioPlayer,
    IOptions<KeywordSpotterOptions> keywordSpotterOptions,
    IOptions<AudioOptions> audioOptions) : BackgroundService
{
    private readonly List<RawPcmChunk> _commandAudioBuffer = [];
    private float _silentDurationMs;
    private DateTime _recordingStartTime = DateTime.MinValue;

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
                keyword => Console.WriteLine($"[listener] Keyword detected: {keyword} \nStarting command recording..."),
                cancellationToken);

            var recordMicTask = Task.Factory.StartNew(
                () => audioInputProvider.WriteAudioInput(audioChannel.Writer, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Console.WriteLine("[listener] VoiceCommandDispatcher started. Listening for wake word...");

            await Task.WhenAll(recordMicTask, audioProcessingTask);
        }
        catch (OperationCanceledException)
        {
            throw; // Let BackgroundService handle graceful shutdown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[listener] Fatal error in VoiceCommandDispatcher: {ex}");
            throw;
        }
    }

    private async Task RunAudioProcessingAsync(
        ChannelReader<byte[]> reader,
        Action<string> onKeywordDetected,
        CancellationToken cancellationToken)
    {
        using var keywordSpotter = CreateKeywordSpotter();
        var keywordStream = keywordSpotter.CreateStream();

        using var stopKeywordSpotter = CreateStopKeywordSpotter();
        var stopKeywordStream = stopKeywordSpotter.CreateStream();

        try
        {
            await foreach (var audioBytes in reader.ReadAllAsync(cancellationToken))
            {
                if (appStateService.CurrentState == VoiceAssistantState.ListeningForWakeWord)
                {
                    ProcessWakeWord(audioBytes, keywordSpotter, keywordStream, onKeywordDetected);
                }

                if (appStateService.CurrentState == VoiceAssistantState.RecordingCommand)
                {
                    await ProcessRecordingAsync(audioBytes, stopKeywordSpotter, stopKeywordStream);
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
            Console.WriteLine($"[listener] Error in inference worker: {ex}");
        }
    }

    private void ProcessWakeWord(
        byte[] audioBytes,
        KeywordSpotter keywordSpotter,
        OnlineStream keywordStream,
        Action<string> onKeywordDetected)
    {
        try
        {
            var chunk = new RawPcmChunk { Bytes = audioBytes, Format = ProcessingAudioFormat };
            var monoSamples = AudioProcessing.ConvertPcm16ToFloat(chunk);
            if (monoSamples.Length == 0)
            {
                return;
            }

            keywordStream.AcceptWaveform(chunk.Format.SampleRate, monoSamples);

            while (keywordSpotter.IsReady(keywordStream))
            {
                keywordSpotter.Decode(keywordStream);

                var keywordResult = keywordSpotter.GetResult(keywordStream);
                if (!string.IsNullOrWhiteSpace(keywordResult.Keyword))
                {
                    appStateService.CurrentState = VoiceAssistantState.RecordingCommand;
                    _commandAudioBuffer.Clear();
                    _silentDurationMs = 0f;
                    _recordingStartTime = DateTime.UtcNow;

                    onKeywordDetected(keywordResult.Keyword);
                    audioPlayer.Queue(Path.Combine(AppContext.BaseDirectory, "Assets", "notification_sound.wav"));
                    keywordSpotter.Reset(keywordStream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[listener] Error during keyword inference: {ex}");
        }
    }

    private async Task ProcessRecordingAsync(
        byte[] audioBytes,
        KeywordSpotter stopKeywordSpotter,
        OnlineStream stopKeywordStream)
    {
        var chunk = new RawPcmChunk { Bytes = audioBytes, Format = ProcessingAudioFormat };
        _commandAudioBuffer.Add(chunk);

        var chunkDurationMs = audioBytes.Length / 32f;
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
                Console.WriteLine($"[listener] Stop keyword detected. Returning to wake word detection.");
                _commandAudioBuffer.Clear();
                _silentDurationMs = 0f;
                audioPlayer.CancelCurrent();
                
                appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
                stopKeywordSpotter.Reset(stopKeywordStream);
                return;
            }
        }

        if (_silentDurationMs >= audioOptions.Value.SilenceTimeoutMs || elapsedMs >= audioOptions.Value.MaxRecordingMs)
        {
            await SendCommandToServerAsync(_commandAudioBuffer);
            _commandAudioBuffer.Clear();
            _silentDurationMs = 0f;
            appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
            Console.WriteLine("[listener] Finished command processing. Returning to wake word detection.");
        }
    }

    private async Task SaveRecordingAsync(List<RawPcmChunk> monoChunks)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "recordings");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"command_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        var wavBytes = AudioProcessing.BuildWavBytes(monoChunks);
        await File.WriteAllBytesAsync(filePath, wavBytes);
        Console.WriteLine($"[listener] Saved: {filePath}");
    }

    private async Task SendCommandToServerAsync(List<RawPcmChunk> monoChunks)
    {
        byte[] wavBytes;
        try
        {
            Console.WriteLine("[listener] Sending command audio to server...");
            wavBytes = AudioProcessing.BuildWavBytes(monoChunks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[listener] Error building WAV data: {ex}");
            return;
        }

        var result = await remoteServerClient.SendCommandAsync(wavBytes);
        if (result.IsSuccess)
        {
            Console.WriteLine("[listener] Command sent successfully. Queuing response audio for playback.");
            audioPlayer.Queue(result.Value.Audio);
        }
        else
        {
            Console.WriteLine($"[listener] Failed to send command: {result.Error}");
            audioPlayer.Queue(Path.Combine(AppContext.BaseDirectory, "Assets", "command_failed.wav"));
        }
    }

    private KeywordSpotter CreateKeywordSpotter()
    {
        var opts = keywordSpotterOptions.Value;
        var modelPath = opts.ModelPath;
        var keywordSpotterConfig = new KeywordSpotterConfig()
        {
            ModelConfig = new OnlineModelConfig()
            {
                Transducer = new OnlineTransducerModelConfig()
                {
                    Encoder = Path.Combine(modelPath, opts.EncoderFile),
                    Decoder = Path.Combine(modelPath, opts.DecoderFile),
                    Joiner = Path.Combine(modelPath, opts.JoinerFile)
                },
                Tokens = Path.Combine(modelPath, opts.TokensPath),
                NumThreads = opts.NumThreads,
                Provider = opts.Provider,
            },
            KeywordsFile = opts.KeywordsFile,
            FeatConfig = new FeatureConfig()
            {
                SampleRate = audioOptions.Value.Input.ProcessingSampleRate,
                FeatureDim = opts.FeatureDim
            }
        };

        return new KeywordSpotter(keywordSpotterConfig);
    }

    private KeywordSpotter CreateStopKeywordSpotter()
    {
        var opts = keywordSpotterOptions.Value;
        var modelPath = opts.ModelPath;
        var keywordSpotterConfig = new KeywordSpotterConfig()
        {
            ModelConfig = new OnlineModelConfig()
            {
                Transducer = new OnlineTransducerModelConfig()
                {
                    Encoder = Path.Combine(modelPath, opts.EncoderFile),
                    Decoder = Path.Combine(modelPath, opts.DecoderFile),
                    Joiner = Path.Combine(modelPath, opts.JoinerFile)
                },
                Tokens = Path.Combine(modelPath, opts.TokensPath),
                NumThreads = opts.NumThreads,
                Provider = opts.Provider,
            },
            KeywordsFile = opts.StopKeywordsFile,
            FeatConfig = new FeatureConfig()
            {
                SampleRate = audioOptions.Value.Input.ProcessingSampleRate,
                FeatureDim = opts.FeatureDim
            }
        };

        return new KeywordSpotter(keywordSpotterConfig);
    }
}
