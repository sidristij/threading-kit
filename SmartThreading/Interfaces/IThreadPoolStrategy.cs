namespace DevTools.Threading
{
    internal interface IThreadPoolStrategy
    {
        ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs);
        
        ParallelismLevelChange RequestForThreadStop(ThreadWrapper executionSegment, int globalQueueCount, int workitemsDone, long range_µs);
    }
}