namespace DevTools.Threading
{
    public interface IThreadPoolThreadsManagement
    {
        int ParallelismLevel { get; }
        
        bool CreateAdditionalExecutionSegments(int count);

        bool NotifyAboutExecutionSegmentStopping(ThreadWrapper segment);
    }
}