using System.Threading.Channels;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.BackgroundProcessing;

// Simple in-process queue backed by an unbounded Channel<Guid>. Good enough for the
// "simple background processing" requirement without pulling in an external broker.
public class InMemoryTaskProcessingQueue : ITaskProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void QueueTask(Guid taskId) => _channel.Writer.TryWrite(taskId);
}
