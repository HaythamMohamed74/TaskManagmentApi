namespace TaskManagement.Application.Interfaces;

public interface ITaskProcessingQueue
{
    void QueueTask(Guid taskId);
}
