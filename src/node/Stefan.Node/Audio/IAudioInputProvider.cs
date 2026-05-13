using System.Threading.Channels;

namespace Stefan.Node.Audio;

public interface IAudioInputProvider
{
    Task WriteAudioInput(ChannelWriter<byte[]> audioWriter, CancellationToken cancellationToken);
}