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
    IOptions<KeywordSpotterOptions> keywordSpotterOptions) : BackgroundService
{
    private const int InputSampleRate = 16_000;
    private const float SilenceThreshold = 0.02f;
    private const int SilenceTimeoutMs = 1000;
    private const int MaxRecordingMs = 10_000;

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
                keyword => Console.WriteLine($"Keyword detected: {keyword} \nStarting command recording..."),
                cancellationToken);

            var recordMicTask = Task.Factory.StartNew(
                () => audioInputProvider.WriteAudioInput(audioChannel.Writer, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Console.WriteLine("VoiceCommandDispatcher started. Listening for wake word...");

            await Task.WhenAll(recordMicTask, audioProcessingTask);
        }
        catch (OperationCanceledException)
        {
            throw; // Let BackgroundService handle graceful shutdown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in VoiceCommandDispatcher: {ex}");
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
                        var monoSamples = ConvertInterleavedStereoPcm16ToMonoFloat(audioBytes);
                        if (monoSamples.Length == 0)
                        {
                            continue;
                        }

                        //Console.WriteLine($"Received audio chunk: {audioBytes.Length} bytes, {monoSamples.Length} mono samples");

                        keywordStream.AcceptWaveform(InputSampleRate, monoSamples);

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
                                keywordSpotter.Reset(keywordStream);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during keyword inference: {ex}");
                    }
                }

                if (appStateService.CurrentState == VoiceAssistantState.RecordingCommand)
                {
                    _commandAudioBuffer.Add(audioBytes);

                    var chunkDurationMs = audioBytes.Length / 64f;
                    var rms = ComputeRms(audioBytes);

                    if (rms < SilenceThreshold)
                    {
                        _silentDurationMs += chunkDurationMs;
                    }
                    else
                    {
                        _silentDurationMs = 0f;
                    }

                    var elapsedMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;

                    if (_silentDurationMs >= SilenceTimeoutMs || elapsedMs >= MaxRecordingMs)
                    {
                        //await SaveRecordingAsync(_commandAudioBuffer);
                        await SendCommandToServerAsync(_commandAudioBuffer);
                        _commandAudioBuffer.Clear();
                        _silentDurationMs = 0f;
                        appStateService.CurrentState = VoiceAssistantState.ListeningForWakeWord;
                        Console.WriteLine("Finished recording command. Returning to wake word detection.");
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
            Console.WriteLine($"Error in inference worker: {ex}");
        }
    }

    private static float[] ConvertInterleavedStereoPcm16ToMonoFloat(ReadOnlySpan<byte> audioBytes)
    {
        var frameCount = audioBytes.Length / (sizeof(short) * 2);
        if (frameCount == 0)
        {
            return [];
        }

        var monoSamples = new float[frameCount];

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var byteOffset = frameIndex * sizeof(short) * 2;
            var left = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(byteOffset, sizeof(short)));
            var right = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(byteOffset + sizeof(short), sizeof(short)));
            monoSamples[frameIndex] = ((left + right) / 2f) / short.MaxValue;
        }

        return monoSamples;
    }

    private static float ComputeRms(ReadOnlySpan<byte> stereoPcm16)
    {
        var frameCount = stereoPcm16.Length / 4;
        if (frameCount == 0) return 0f;

        var sumSquares = 0.0;
        for (var i = 0; i < frameCount; i++)
        {
            var offset = i * 4;
            var left = (double)BinaryPrimitives.ReadInt16LittleEndian(stereoPcm16.Slice(offset, 2));
            var right = (double)BinaryPrimitives.ReadInt16LittleEndian(stereoPcm16.Slice(offset + 2, 2));
            var mono = (left + right) / 2.0;
            sumSquares += mono * mono;
        }

        return (float)(Math.Sqrt(sumSquares / frameCount) / short.MaxValue);
    }

    private static byte[] ConvertStereoToMonoPcm16(List<byte[]> chunks)
    {
        var totalFrames = 0;
        foreach (var chunk in chunks)
            totalFrames += chunk.Length / 4;

        var monoBytes = new byte[totalFrames * 2];
        var monoOffset = 0;

        foreach (var chunk in chunks)
        {
            var frameCount = chunk.Length / 4;
            for (var i = 0; i < frameCount; i++)
            {
                var offset = i * 4;
                var left = BinaryPrimitives.ReadInt16LittleEndian(chunk.AsSpan(offset, 2));
                var right = BinaryPrimitives.ReadInt16LittleEndian(chunk.AsSpan(offset + 2, 2));
                var mono = (short)((left + right) / 2);
                BinaryPrimitives.WriteInt16LittleEndian(monoBytes.AsSpan(monoOffset), mono);
                monoOffset += 2;
            }
        }

        return monoBytes;
    }

    private static async Task SaveRecordingAsync(List<byte[]> stereoChunks)
    {
        var monoData = ConvertStereoToMonoPcm16(stereoChunks);
        var dir = Path.Combine(AppContext.BaseDirectory, "recordings");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"command_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        fs.Write(CreateWavHeader(monoData.Length, InputSampleRate));
        await fs.WriteAsync(monoData);

        Console.WriteLine($"Saved: {filePath}");
    }

    private async Task SendCommandToServerAsync(List<byte[]> stereoChunks)
    {
        try
        {
            Console.WriteLine("Sending command audio to server...");

            var monoData = ConvertStereoToMonoPcm16(stereoChunks);
            var wavHeader = CreateWavHeader(monoData.Length, InputSampleRate);
            var wavBytes = new byte[wavHeader.Length + monoData.Length];
            wavHeader.CopyTo(wavBytes, 0);
            monoData.CopyTo(wavBytes, wavHeader.Length);

            var responseAudio = await remoteServerClient.SendCommandAsync(wavBytes);
            if (responseAudio is not null)
            {
                Console.WriteLine("Command sent successfully. Queuing response audio for playback.");
                audioPlayer.Queue(responseAudio);
            }
            else
            {
                Console.WriteLine("Failed to send command.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command to server: {ex}");
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
                NumThreads = 2,
                Provider = "cpu",
            },
            KeywordsFile = Path.Combine(modelPath, opts.KeywordsFile),
            FeatConfig = new FeatureConfig()
            {
                SampleRate = 16000,
                FeatureDim = 80
            }
        };

        return new KeywordSpotter(keywordSpotterConfig);
    }
}
