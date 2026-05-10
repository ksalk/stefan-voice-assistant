using System.Buffers.Binary;
using System.Threading.Channels;
using Alsa.Net;
using SherpaOnnx;

public class VoiceCommandDispatcher : BackgroundService
{
    const int InputSampleRate = 16_000;
    const int BufferSeconds = 3;
    const int SnapshotWindowSeconds = 1;
    // const int LogEveryNCallbacks = 200;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var soundDeviceSettings = new SoundDeviceSettings()
            {
                RecordingDeviceName = "plughw:0,0", // software resampling if hw doesn't support 16kHz natively
                RecordingSampleRate = InputSampleRate,
                RecordingChannels = 2,
                RecordingBitsPerSample = 16
            };

            using var alsaDevice = AlsaDeviceBuilder.Create(soundDeviceSettings);

            var buffer = new SlidingAudioBuffer(InputSampleRate * BufferSeconds);
            var latestWindowBytes = new byte[InputSampleRate * SnapshotWindowSeconds * sizeof(short)];
            using var keywordSpotter = CreateKeywordSpotter();
            var keywordStream = keywordSpotter.CreateStream();

            var audioChannel = Channel.CreateBounded<float[]>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true
                });

            var inferenceTask = RunInferenceAsync(
                audioChannel.Reader,
                keywordSpotter,
                keywordStream,
                cancellationToken);

            var recordTask = Task.Run(() =>
            {
                try
                {
                    // var callbackCount = 0;

                    alsaDevice.Record(audioBytes =>
                    {
                        try
                        {
                            buffer.AppendInterleavedStereoPcm16(audioBytes);

                            var monoSamples = ConvertInterleavedStereoPcm16ToMonoFloat(audioBytes);
                            if (monoSamples.Length > 0)
                            {
                                audioChannel.Writer.TryWrite(monoSamples);
                            }

                            // callbackCount++;
                            // if (callbackCount % LogEveryNCallbacks != 0)
                            // {
                            //     return;
                            // }

                            // var copiedBytes = buffer.CopyLatestBytesTo(latestWindowBytes);
                            // var bufferedSamples = buffer.CountSamples;
                            // Console.WriteLine(
                            //     $"Buffered {bufferedSamples} mono samples ({bufferedSamples / (double)InputSampleRate:F2}s) - latest window {copiedBytes} bytes");
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
                    audioChannel.Writer.Complete();
                }
            }, cancellationToken);

            await Task.WhenAll(recordTask, inferenceTask);
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

    private static async Task RunInferenceAsync(
        ChannelReader<float[]> reader,
        KeywordSpotter keywordSpotter,
        OnlineStream keywordStream,
        CancellationToken cancellationToken)
    {
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
                            Console.WriteLine($"Detected keyword: {keywordResult.Keyword}");
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
