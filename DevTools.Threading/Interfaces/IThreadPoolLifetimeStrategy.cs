namespace DevTools.Threading
{
    public interface IThreadPoolLifetimeStrategy
    {
        bool RequestForThreadStop(IExecutionSegment executionSegment, int globalQueueCount, int workitemsDone,
            double timeSpanMs);
        
        void RequestForThreadStartIfNeed(int globalQueueCount, int workitemsDone, double timeSpanMs);
    }
}