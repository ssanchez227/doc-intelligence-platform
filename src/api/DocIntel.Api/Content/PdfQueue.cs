using System.Threading.Channels;

namespace DocIntel.Api.Content;

// Published versions waiting for their PDF (UC-07). In memory: if the API stops
// before converting, the converter finds the version again at startup, because
// its PdfStatus is still Pending in the database.
public sealed class PdfQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid versionId) => _channel.Writer.TryWrite(versionId);

    public ChannelReader<Guid> Reader => _channel.Reader;
}
