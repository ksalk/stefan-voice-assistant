using System.Buffers.Binary;
using Stefan.Node.Audio;

namespace Stefan.Node.UnitTests.Audio;

public class AudioProcessingTests
{
    #region Helpers

    private static RawPcmChunk CreateRawPcmChunk(byte[] bytes, AudioFormat format)
    {
        return new RawPcmChunk { Bytes = bytes, Format = format };
    }

    private static byte[] ToLittleEndianBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        }
        return bytes;
    }

    private static byte[] ToInterleavedBytes(params short[][] channels)
    {
        if (channels.Length == 0)
        {
            return [];
        }

        var frameCount = channels[0].Length;
        foreach (var channel in channels)
        {
            if (channel.Length != frameCount)
            {
                throw new ArgumentException("All channels must have the same number of samples.");
            }
        }

        var bytes = new byte[frameCount * channels.Length * 2];
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var ch = 0; ch < channels.Length; ch++)
            {
                var offset = (frame * channels.Length + ch) * 2;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), channels[ch][frame]);
            }
        }
        return bytes;
    }

    #endregion

    #region ConvertToMono

    [Fact]
    public void ConvertToMono_MonoInput_ReturnsSameSamples()
    {
        var samples = new short[] { 0, 1000, -1000, short.MaxValue, short.MinValue };
        var input = CreateRawPcmChunk(ToLittleEndianBytes(samples), new AudioFormat
        {
            Channels = 1,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertToMono(input);

        Assert.Equal(samples, result.Samples);
        Assert.Equal(16000, result.SampleRate);
    }

    [Fact]
    public void ConvertToMono_StereoInput_AveragesChannels()
    {
        var left = new short[] { 1000, 2000 };
        var right = new short[] { 3000, 4000 };
        var input = CreateRawPcmChunk(ToInterleavedBytes(left, right), new AudioFormat
        {
            Channels = 2,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertToMono(input);

        Assert.Equal(new short[] { 2000, 3000 }, result.Samples);
        Assert.Equal(16000, result.SampleRate);
    }

    [Fact]
    public void ConvertToMono_MultiChannelInput_AveragesChannels()
    {
        var ch1 = new short[] { 1000, 2000 };
        var ch2 = new short[] { 2000, 3000 };
        var ch3 = new short[] { 3000, 4000 };
        var ch4 = new short[] { 4000, 5000 };
        var input = CreateRawPcmChunk(ToInterleavedBytes(ch1, ch2, ch3, ch4), new AudioFormat
        {
            Channels = 4,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertToMono(input);

        Assert.Equal(new short[] { 2500, 3500 }, result.Samples);
        Assert.Equal(16000, result.SampleRate);
    }

    [Fact]
    public void ConvertToMono_EmptyInput_ReturnsEmpty()
    {
        var input = CreateRawPcmChunk([], new AudioFormat
        {
            Channels = 1,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertToMono(input);

        Assert.Empty(result.Samples);
        Assert.Equal(16000, result.SampleRate);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    [InlineData(32)]
    public void ConvertToMono_Non16Bit_ThrowsNotSupportedException(int bitsPerSample)
    {
        var input = CreateRawPcmChunk([0, 0], new AudioFormat
        {
            Channels = 1,
            SampleRate = 16000,
            BitsPerSample = bitsPerSample
        });

        var ex = Assert.Throws<NotSupportedException>(() => AudioProcessing.ConvertToMono(input));
        Assert.Contains($"{bitsPerSample}", ex.Message);
    }

    [Fact]
    public void ConvertToMono_MisalignedBuffer_ThrowsArgumentException()
    {
        var input = CreateRawPcmChunk([0, 0, 0], new AudioFormat
        {
            Channels = 2,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        Assert.Throws<ArgumentException>(() => AudioProcessing.ConvertToMono(input));
    }

    #endregion

    #region Resample

    [Fact]
    public void Resample_SameSampleRate_ReturnsSameSamples()
    {
        var samples = new short[] { 100, 200, 300, 400 };
        var input = new MonoPcmSamples { Samples = samples, SampleRate = 16000 };

        var result = AudioProcessing.Resample(input, 16000);

        Assert.Same(samples, result.Samples);
        Assert.Equal(16000, result.SampleRate);
    }

    [Fact]
    public void Resample_Downsample_ProducesExpectedLength()
    {
        var input = new MonoPcmSamples { Samples = new short[9], SampleRate = 48000 };

        var result = AudioProcessing.Resample(input, 16000);

        Assert.Equal(3, result.Samples.Length);
        Assert.Equal(16000, result.SampleRate);
    }

    [Fact]
    public void Resample_Upsample_ProducesExpectedLength()
    {
        var input = new MonoPcmSamples { Samples = new short[3], SampleRate = 16000 };

        var result = AudioProcessing.Resample(input, 48000);

        Assert.Equal(9, result.Samples.Length);
        Assert.Equal(48000, result.SampleRate);
    }

    [Fact]
    public void Resample_DcSignal_RemainsConstant()
    {
        var input = new MonoPcmSamples { Samples = new short[] { 1000, 1000, 1000, 1000 }, SampleRate = 16000 };

        var result = AudioProcessing.Resample(input, 48000);

        Assert.All(result.Samples, sample => Assert.Equal(1000, sample));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resample_InvalidTargetRate_ThrowsArgumentException(int targetRate)
    {
        var input = new MonoPcmSamples { Samples = [100], SampleRate = 16000 };

        Assert.Throws<ArgumentException>(() => AudioProcessing.Resample(input, targetRate));
    }

    [Fact]
    public void Resample_ShortInput_HandlesCorrectly()
    {
        var input = new MonoPcmSamples { Samples = [1000], SampleRate = 16000 };

        var result = AudioProcessing.Resample(input, 48000);

        Assert.Equal(3, result.Samples.Length);
        Assert.All(result.Samples, sample => Assert.Equal(1000, sample));
    }

    [Fact]
    public void Resample_BoundaryValues_DoNotOverflow()
    {
        var input = new MonoPcmSamples { Samples = [short.MaxValue, short.MinValue, short.MaxValue], SampleRate = 16000 };

        var result = AudioProcessing.Resample(input, 48000);

        Assert.All(result.Samples, sample => Assert.InRange(sample, short.MinValue, short.MaxValue));
    }

    #endregion

    #region ConvertSamplesToBytes

    [Fact]
    public void ConvertSamplesToBytes_PositiveSamples_RoundTripCorrectly()
    {
        var samples = new short[] { 0, 1, 1000, short.MaxValue };
        var input = new MonoPcmSamples { Samples = samples, SampleRate = 16000 };

        var result = AudioProcessing.ConvertSamplesToBytes(input);

        Assert.Equal(samples.Length * 2, result.Bytes.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], BinaryPrimitives.ReadInt16LittleEndian(result.Bytes.AsSpan(i * 2, 2)));
        }
    }

    [Fact]
    public void ConvertSamplesToBytes_NegativeSamples_RoundTripCorrectly()
    {
        var samples = new short[] { -1, -1000, short.MinValue };
        var input = new MonoPcmSamples { Samples = samples, SampleRate = 16000 };

        var result = AudioProcessing.ConvertSamplesToBytes(input);

        Assert.Equal(samples.Length * 2, result.Bytes.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], BinaryPrimitives.ReadInt16LittleEndian(result.Bytes.AsSpan(i * 2, 2)));
        }
    }

    [Fact]
    public void ConvertSamplesToBytes_EmptyInput_ReturnsEmptyChunk()
    {
        var input = new MonoPcmSamples { Samples = [], SampleRate = 16000 };

        var result = AudioProcessing.ConvertSamplesToBytes(input);

        Assert.Empty(result.Bytes);
        Assert.Equal(1, result.Format.Channels);
        Assert.Equal(16, result.Format.BitsPerSample);
        Assert.Equal(16000, result.Format.SampleRate);
    }

    [Fact]
    public void ConvertSamplesToBytes_SetsCorrectFormat()
    {
        var input = new MonoPcmSamples { Samples = [1, 2, 3], SampleRate = 48000 };

        var result = AudioProcessing.ConvertSamplesToBytes(input);

        Assert.Equal(1, result.Format.Channels);
        Assert.Equal(16, result.Format.BitsPerSample);
        Assert.Equal(48000, result.Format.SampleRate);
    }

    #endregion

    #region ConvertAndResample

    [Fact]
    public void ConvertAndResample_Stereo48000ToMono16000_ProducesExpectedOutput()
    {
        var left = new short[] { 1000, 2000, 3000, 4000, 5000, 6000 };
        var right = new short[] { 3000, 4000, 5000, 6000, 7000, 8000 };
        var input = CreateRawPcmChunk(ToInterleavedBytes(left, right), new AudioFormat
        {
            Channels = 2,
            SampleRate = 48000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertAndResample(input, 16000);

        Assert.Equal(1, result.Format.Channels);
        Assert.Equal(16, result.Format.BitsPerSample);
        Assert.Equal(16000, result.Format.SampleRate);
        // 6 mono samples / (48000/16000) = 2 samples
        Assert.Equal(2, result.Bytes.Length / 2);
    }

    [Fact]
    public void ConvertAndResample_MonoSameRate_ReturnsEquivalentData()
    {
        var samples = new short[] { 100, 200, 300 };
        var input = CreateRawPcmChunk(ToLittleEndianBytes(samples), new AudioFormat
        {
            Channels = 1,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertAndResample(input, 16000);

        Assert.Equal(samples.Length * 2, result.Bytes.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], BinaryPrimitives.ReadInt16LittleEndian(result.Bytes.AsSpan(i * 2, 2)));
        }
    }

    [Fact]
    public void ConvertAndResample_EmptyInput_ReturnsEmptyOutput()
    {
        var input = CreateRawPcmChunk([], new AudioFormat
        {
            Channels = 1,
            SampleRate = 16000,
            BitsPerSample = 16
        });

        var result = AudioProcessing.ConvertAndResample(input, 16000);

        Assert.Empty(result.Bytes);
    }

    #endregion
}
