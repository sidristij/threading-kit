namespace DevTools.Threading
{
    public interface IThreadPoolLifetimeStrategy
    {
        bool RequestForThreadStop(IExecutionSegment executionSegment, int globalQueueCount, int workitemsDone, long range_µs);
        
        void RequestForThreadStartIfNeed(int globalQueueCount, int workItemsDone, long range_µs);
    }
}