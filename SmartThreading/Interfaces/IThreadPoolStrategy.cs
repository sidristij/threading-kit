namespace DevTools.Threading
{
    public interface IThreadPoolStrategy
    {
        void Initialize(IThreadPoolThreadsManagement threadsManagement);
        
        IThreadPoolThreadStrategy CreateThreadStrategy(ThreadWrappingQueue threadWrappingQueue);
        
        ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs);
        
        ParallelismLevelChange RequestForThreadStop(ThreadWrappingQueue threadWrappingQueue, int globalQueueCount, int workitemsDone, long range_µs);
    }
}