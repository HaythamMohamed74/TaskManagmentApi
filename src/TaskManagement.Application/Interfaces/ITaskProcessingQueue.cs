namespace TaskManagement.Application.Interfaces;

// Abstraction over the background processing mechanism. Enqueuing a task id
// hands it off to a worker (see TaskProcessingBackgroundService in Infrastructure)
// that simulates processing and updates the task's status.
public interface ITaskProcessingQueue
{
    void QueueTask(Guid taskId);
}
