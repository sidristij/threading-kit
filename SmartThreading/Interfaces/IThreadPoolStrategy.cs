namespace DevTools.Threading
{
    public interface IThreadPoolStrategy
    {
        ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs);
        
        ParallelismLevelChange RequestForThreadStop(IExecutionSegment executionSegment, int globalQueueCount, int workitemsDone, long range_µs);
    }
}