namespace DevTools.Threading
{
    public interface IThreadPoolStrategy
    {
        void Initialize(IThreadPoolThreadsManagement threadsManagement);
        
        IThreadPoolThreadStrategy CreateThreadStrategy(ThreadWrapper threadWrapper);
        
        ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs);
        
        ParallelismLevelChange RequestForThreadStop(ThreadWrapper threadWrapper, int globalQueueCount, int workitemsDone, long range_µs);
    }
}