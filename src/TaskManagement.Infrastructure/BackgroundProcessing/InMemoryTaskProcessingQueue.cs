using System.Threading.Channels;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.BackgroundProcessing;

public class InMemoryTaskProcessingQueue : ITaskProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void QueueTask(Guid taskId) => _channel.Writer.TryWrite(taskId);
}
