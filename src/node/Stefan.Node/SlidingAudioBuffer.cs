using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal sealed class SlidingAudioBuffer
{
    private readonly short[] _samples;
    private readonly object _sync = new();
    private int _writeIndex;
    private int _count;

    public SlidingAudioBuffer(int capacitySamples)
    {
        if (capacitySamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacitySamples));
        }

        _samples = new short[capacitySamples];
    }

    public int CapacitySamples => _samples.Length;

    public int CountSamples
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    public void AppendInterleavedStereoPcm16(ReadOnlySpan<byte> audioBytes)
    {
        var frameCount = audioBytes.Length / (sizeof(short) * 2);
        if (frameCount == 0)
        {
            return;
        }

        lock (_sync)
        {
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var byteOffset = frameIndex * sizeof(short) * 2;
                var left = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(byteOffset, sizeof(short)));
                var right = BinaryPrimitives.ReadInt16LittleEndian(audioBytes.Slice(byteOffset + sizeof(short), sizeof(short)));
                var mixed = (short)((left + right) / 2);

                _samples[_writeIndex] = mixed;
                _writeIndex = (_writeIndex + 1) % _samples.Length;

                if (_count < _samples.Length)
                {
                    _count++;
                }
            }
        }
    }

    public int CopyLatestSamplesTo(Span<short> destination)
    {
        lock (_sync)
        {
            var samplesToCopy = Math.Min(destination.Length, _count);
            if (samplesToCopy == 0)
            {
                return 0;
            }

            var start = (_writeIndex - samplesToCopy + _samples.Length) % _samples.Length;
            var firstSegmentLength = Math.Min(samplesToCopy, _samples.Length - start);
            _samples.AsSpan(start, firstSegmentLength).CopyTo(destination);

            var remaining = samplesToCopy - firstSegmentLength;
            if (remaining > 0)
            {
                _samples.AsSpan(0, remaining).CopyTo(destination[firstSegmentLength..]);
            }

            return samplesToCopy;
        }
    }

    public int CopyLatestBytesTo(Span<byte> destination)
    {
        var destinationSamples = MemoryMarshal.Cast<byte, short>(destination);
        return CopyLatestSamplesTo(destinationSamples) * sizeof(short);
    }
}