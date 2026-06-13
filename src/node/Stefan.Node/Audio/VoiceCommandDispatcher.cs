using System.Buffers.Binary;
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
    private readonly List<byte[]> _commandAudioBuffer = [];
    private float _silentDurationMs;
    private DateTime _recordingStartTime = DateTime.MinValue;

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

        // fields moved to class level

        try
        {
            await foreach (var audioBytes in reader.ReadAllAsync(cancellationToken))
            {
                if (appStateService.CurrentState == VoiceAssistantState.ListeningForWakeWord)
                {
                    try
                    {
                        var monoSamples = ConvertPcm16ToFloat(audioBytes);
                        if (monoSamples.Length == 0)
                        {
                            continue;
                        }

                        keywordStream.AcceptWaveform(audioOptions.Value.Input.ProcessingSampleRate, monoSamples);

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

                if (appStateService.CurrentState == VoiceAssistantState.RecordingCommand)
                {
                    _commandAudioBuffer.Add(audioBytes);

                    var chunkDurationMs = audioBytes.Length / 32f;
                    var rms = ComputeRms(audioBytes);

                    if (rms < audioOptions.Value.SilenceThreshold)
                    {
                        _silentDurationMs += chunkDurationMs;
                    }
                    else
                    {
                        _silentDurationMs = 0f;
                    }

                    var elapsedMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;

                    if (_silentDurationMs >= audioOptions.Value.SilenceTimeoutMs || elapsedMs >= audioOptions.Value.MaxRecordingMs)
                    {
                        //await SaveRecordingAsync(_commandAudioBuffer);
                        await SendCommandToServerAsync(_commandAudioBuffer);
                        _commandAudioBuffer.Clear();
                        _silentDurationMs = 0f;
                        appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
                        Console.WriteLine("[listener] Finished recording command. Returning to wake word detection.");
                    }
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

    private static float[] ConvertPcm16ToFloat(ReadOnlySpan<byte> audioBytes)
    {
        var sampleCount = audioBytes.Length / sizeof(short);
        if (sampleCount == 0)
        {
            return [];
        }

        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(i * sizeof(short), sizeof(short)));
            samples[i] = sample / (float)short.MaxValue;
        }

        return samples;
    }

    private static float ComputeRms(ReadOnlySpan<byte> monoPcm16)
    {
        var sampleCount = monoPcm16.Length / 2;
        if (sampleCount == 0) return 0f;

        var sumSquares = 0.0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (double)BinaryPrimitives.ReadInt16LittleEndian(monoPcm16.Slice(i * 2, 2));
            sumSquares += sample * sample;
        }

        return (float)(Math.Sqrt(sumSquares / sampleCount) / short.MaxValue);
    }



    private async Task SaveRecordingAsync(List<byte[]> monoChunks)
    {
        var totalBytes = 0;
        foreach (var chunk in monoChunks)
            totalBytes += chunk.Length;

        var monoData = new byte[totalBytes];
        var offset = 0;
        foreach (var chunk in monoChunks)
        {
            chunk.CopyTo(monoData, offset);
            offset += chunk.Length;
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "recordings");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"command_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        fs.Write(CreateWavHeader(monoData.Length, audioOptions.Value.Input.ProcessingSampleRate));
        await fs.WriteAsync(monoData);

        Console.WriteLine($"[listener] Saved: {filePath}");
    }

    private async Task SendCommandToServerAsync(List<byte[]> monoChunks)
    {
        byte[] wavBytes;
        try
        {
            Console.WriteLine("[listener] Sending command audio to server...");

            var totalBytes = 0;
            foreach (var chunk in monoChunks)
                totalBytes += chunk.Length;

            var monoData = new byte[totalBytes];
            var offset = 0;
            foreach (var chunk in monoChunks)
            {
                chunk.CopyTo(monoData, offset);
                offset += chunk.Length;
            }

            var wavHeader = CreateWavHeader(monoData.Length, audioOptions.Value.Input.ProcessingSampleRate);
            wavBytes = new byte[wavHeader.Length + monoData.Length];
            wavHeader.CopyTo(wavBytes, 0);
            monoData.CopyTo(wavBytes, wavHeader.Length);
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
        }
    }

    private static byte[] CreateWavHeader(int dataSize, int sampleRate)
    {
        var h = new byte[44];
        var s = h.AsSpan();
        "RIFF"u8.CopyTo(s);
        BinaryPrimitives.WriteInt32LittleEndian(s[4..], 36 + dataSize);
        "WAVE"u8.CopyTo(s[8..]);
        "fmt "u8.CopyTo(s[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(s[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(s[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(s[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(s[28..], sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[34..], 16);
        "data"u8.CopyTo(s[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[40..], dataSize);
        return h;
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
}
