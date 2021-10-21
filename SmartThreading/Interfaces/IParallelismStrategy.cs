namespace DevTools.Threading
{
    public interface IParallelismStrategy
    {
        void Initialize(IThreadPoolThreadsManagement threadsManagement);
        
        IParallelismLocalStrategy CreateLocalStrategy(ThreadWrappingQueue threadWrappingQueue);
        
        ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs);
        
        ParallelismLevelChange RequestForThreadStop(ThreadWrappingQueue threadWrappingQueue, int globalQueueCount, int workitemsDone, long range_µs);
    }
}