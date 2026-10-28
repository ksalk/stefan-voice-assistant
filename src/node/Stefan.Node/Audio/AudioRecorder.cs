using Microsoft.Extensions.Options;
using Stefan.Node.Options;

namespace Stefan.Node.Audio;

/// <summary>
/// Records incoming audio streams to WAV files on disk.
/// </summary>
/// <remarks>
/// <para>The node can capture two streams:</para>
/// <list type="bullet">
/// <item>
///   <term>Raw</term>
///   <description>The bytes coming straight from the microphone or pipe, in the configured input format (e.g. 24/48 kHz).</description>
/// </item>
/// <item>
///   <term>Processed</term>
///   <description>The mono 16-bit audio at the processing sample rate that the wake-word spotter normally sees.</description>
/// </item>
/// </list>
/// <para>Each enabled stream is written to one file per run. Files are created lazily on the first chunk,
/// so a disabled recorder never touches the disk. The WAV header starts with a placeholder size and is
/// refreshed once per second of audio, so the file stays playable even after a hard crash. The final
/// header is written when the recorder is disposed at shutdown.</para>
/// </remarks>
public class AudioRecorder(IOptions<AudioOptions> audioOptions, ILogger<AudioRecorder> logger) : IDisposable
{
    private readonly AudioRecordingOptions _options = audioOptions.Value.Recording;
    private readonly string _baseDirectory = Path.Combine(AppContext.BaseDirectory, audioOptions.Value.Recording.Directory);
    private readonly object _rawLock = new();
    private readonly object _processedLock = new();

    private WavFileStream? _rawFile;
    private WavFileStream? _processedFile;

    /// <summary>Appends one chunk of raw microphone or pipe input, in the device format.</summary>
    public void WriteRaw(RawPcmChunk chunk)
    {
        if (!_options.Enabled || !_options.IncludeRaw)
        {
            return;
        }

        lock (_rawLock)
        {
            _rawFile ??= new WavFileStream(NextPath("raw", chunk.Format.SampleRate), chunk.Format, "raw mic input", logger);
            _rawFile.Write(chunk.Bytes);
        }
    }

    /// <summary>Appends one chunk of processed mono 16-bit audio, at the processing sample rate.</summary>
    public void WriteProcessed(RawPcmChunk chunk)
    {
        if (!_options.Enabled || !_options.IncludeProcessed)
        {
            return;
        }

        lock (_processedLock)
        {
            _processedFile ??= new WavFileStream(NextPath("processed", chunk.Format.SampleRate), chunk.Format, "processed input", logger);
            _processedFile.Write(chunk.Bytes);
        }
    }

    public void Dispose()
    {
        lock (_rawLock)
        {
            _rawFile?.Dispose();
            _rawFile = null;
        }

        lock (_processedLock)
        {
            _processedFile?.Dispose();
            _processedFile = null;
        }
    }

    private string NextPath(string kind, int sampleRate)
    {
        return Path.Combine(_baseDirectory, $"mic_{kind}_{sampleRate}hz_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav");
    }

    /// <summary>
    /// Writes one WAV file. Appends PCM data and keeps the 44-byte RIFF/WAV header up to date,
    /// so the file remains a valid WAV even if the process dies without a graceful shutdown.
    /// </summary>
    private sealed class WavFileStream(string path, AudioFormat format, string streamName, ILogger logger) : IDisposable
    {
        // The WAV header stores sizes as 32-bit values, so one file cannot exceed ~2 GB of PCM data.
        private const long MaxDataBytes = int.MaxValue - 44;

        private readonly FileStream _stream = Open(path, format);
        private readonly int _bytesPerSecond = format.SampleRate * format.Channels * (format.BitsPerSample / 8);
        private long _dataBytes;
        private long _bytesSinceHeaderRefresh;
        private bool _loggedSizeLimit;

        /// <summary>Appends PCM bytes to the file. Not thread-safe; callers must hold the owning lock.</summary>
        public void Write(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return;
            }

            if (_dataBytes >= MaxDataBytes)
            {
                if (!_loggedSizeLimit)
                {
                    _loggedSizeLimit = true;
                    logger.LogWarning("[recorder] {Stream} reached the WAV size limit; further audio is dropped: {Path}", streamName, path);
                }
                return;
            }

            var bytesToWrite = (int)Math.Min(bytes.Length, MaxDataBytes - _dataBytes);
            _stream.Write(bytes, 0, bytesToWrite);
            _dataBytes += bytesToWrite;
            _bytesSinceHeaderRefresh += bytesToWrite;

            if (_bytesSinceHeaderRefresh >= _bytesPerSecond)
            {
                RefreshHeader();
            }
        }

        public void Dispose()
        {
            try
            {
                RefreshHeader();
                _stream.Flush();
                var durationSeconds = _bytesPerSecond > 0 ? _dataBytes / (double)_bytesPerSecond : 0;
                logger.LogInformation("[recorder] Saved {Stream}: {Path} ({Duration:F1}s)", streamName, path, durationSeconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[recorder] Failed to finalize {Stream} recording: {Path}", streamName, path);
            }
            finally
            {
                _stream.Dispose();
            }
        }

        private static FileStream Open(string path, AudioFormat format)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                WriteHeader(stream, dataBytes: 0, format);
                stream.Flush(); // Make the file a valid (empty) WAV before any audio arrives
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            return stream;
        }

        private void RefreshHeader()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            WriteHeader(_stream, (int)_dataBytes, format);
            _stream.Seek(0, SeekOrigin.End);
            _stream.Flush();
            _bytesSinceHeaderRefresh = 0;
        }

        private static void WriteHeader(FileStream stream, int dataBytes, AudioFormat format)
        {
            var header = AudioProcessing.CreateWavHeader(dataBytes, format.SampleRate, format.Channels, format.BitsPerSample);
            stream.Write(header, 0, header.Length);
        }
    }
}
