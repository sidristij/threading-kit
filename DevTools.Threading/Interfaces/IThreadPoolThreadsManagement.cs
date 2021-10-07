namespace DevTools.Threading
{
    public interface IThreadPoolThreadsManagement
    {
        int ParallelismLevel { get; }
        
        bool CreateAdditionalThread();
        bool CheckCanStopThread();

        void NotifyExecutionSegmentStopped(IExecutionSegment segment);
    }
}