using System.Buffers.Binary;
using System.Threading.Channels;
using Alsa.Net;
using SherpaOnnx;

public class VoiceCommandDispatcher : BackgroundService
{
    private const int InputSampleRate = 16_000;
    private const float SilenceThreshold = 0.02f;
    private const int SilenceTimeoutMs = 1000;
    private const int MaxRecordingMs = 10_000;

    private State _state = State.ListeningForWakeWord;
    private readonly List<byte[]> _commandAudioBuffer = [];
    private float _silentDurationMs;
    private DateTime _recordingStartTime = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _state = State.ListeningForWakeWord;
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
                () => RunMicRecording(audioChannel.Writer, cancellationToken),
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
                if (_state == State.ListeningForWakeWord)
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
                                _state = State.RecordingCommand;
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

                if (_state == State.RecordingCommand)
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
                        await SaveRecordingAsync(_commandAudioBuffer);
                        _commandAudioBuffer.Clear();
                        _silentDurationMs = 0f;
                        _state = State.ListeningForWakeWord;
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

    // TODO: extract to IAudioInputProvider or sth
    private Task RunMicRecording(ChannelWriter<byte[]> writer, CancellationToken cancellationToken)
    {
        // TODO: Make sound device settings configurable and discover devices
        var soundDeviceSettings = new SoundDeviceSettings()
        {
            RecordingDeviceName = "plughw:1,0", // software resampling if hw doesn't support 16kHz natively
            RecordingSampleRate = InputSampleRate,
            RecordingChannels = 2,
            RecordingBitsPerSample = 16
        };

        using var alsaDevice = AlsaDeviceBuilder.Create(soundDeviceSettings);
        try
        {
            alsaDevice.Record(audioBytes =>
            {
                try
                {
                    writer.TryWrite(audioBytes.ToArray());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ALSA record callback: {ex}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ALSA record loop: {ex}");
        }
        finally
        {
            writer.Complete();
        }

        return Task.CompletedTask;
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
        const string baseModelPath = @"/home/ksalk/dev/sherpa-onnx-models/sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20";
        var keywordsPath = Path.Combine(AppContext.BaseDirectory, "keywords.txt");
        var keywordSpotterConfig = new KeywordSpotterConfig()
        {
            ModelConfig = new OnlineModelConfig()
            {
                Transducer = new OnlineTransducerModelConfig()
                {
                    Encoder = Path.Combine(baseModelPath, "encoder-epoch-13-avg-2-chunk-16-left-64.onnx"),
                    Decoder = Path.Combine(baseModelPath, "decoder-epoch-13-avg-2-chunk-16-left-64.onnx"),
                    Joiner = Path.Combine(baseModelPath, "joiner-epoch-13-avg-2-chunk-16-left-64.onnx")
                },
                Tokens = Path.Combine(baseModelPath, "tokens.txt"),
                NumThreads = 2,
                Provider = "cpu",
            },
            KeywordsFile = keywordsPath,
            FeatConfig = new FeatureConfig()
            {
                SampleRate = 16000,
                FeatureDim = 80
            }
        };

        return new KeywordSpotter(keywordSpotterConfig);
    }
}

public enum State
{
    ListeningForWakeWord,
    RecordingCommand
}