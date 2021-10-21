namespace DevTools.Threading
{
    public interface IThreadPoolThreadsManagement
    {
        int ParallelismLevel { get; }
        
        bool CreateThreadWrappingQueue(int count);

        bool NotifyThreadWrappingQueueStopping(ThreadWrappingQueue queue);
    }
}