using System.Buffers.Binary;
using System.Threading.Channels;
using Alsa.Net;
using SherpaOnnx;

public class VoiceCommandDispatcher : BackgroundService
{
    const int InputSampleRate = 16_000;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var audioChannel = Channel.CreateBounded<float[]>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true
                });

            var wakeWordDetectionTask = RunWakeWordDetectionAsync(
                audioChannel.Reader,
                keyword => Console.WriteLine($"Keyword detected: {keyword}"),
                cancellationToken);

            var recordMicTask = Task.Factory.StartNew(
                () => RunMicRecording(audioChannel.Writer, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            await Task.WhenAll(recordMicTask, wakeWordDetectionTask);
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

    private async Task RunWakeWordDetectionAsync(
        ChannelReader<float[]> reader,
        Action<string> onKeywordDetected,
        CancellationToken cancellationToken)
    {
        using var keywordSpotter = CreateKeywordSpotter();
            var keywordStream = keywordSpotter.CreateStream();

        try
        {
            await foreach (var monoSamples in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    keywordStream.AcceptWaveform(InputSampleRate, monoSamples);

                    while (keywordSpotter.IsReady(keywordStream))
                    {
                        keywordSpotter.Decode(keywordStream);

                        var keywordResult = keywordSpotter.GetResult(keywordStream);
                        if (!string.IsNullOrWhiteSpace(keywordResult.Keyword))
                        {
                            //Console.WriteLine($"Detected keyword: {keywordResult.Keyword}");
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

    private Task RunMicRecording(ChannelWriter<float[]> writer, CancellationToken cancellationToken)
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
                    var monoSamples = ConvertInterleavedStereoPcm16ToMonoFloat(audioBytes);
                    if (monoSamples.Length > 0)
                    {
                        writer.TryWrite(monoSamples);
                    }
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
