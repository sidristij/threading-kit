namespace DevTools.Threading
{
    public interface IThreadPoolThreadsManagement
    {
        int ParallelismLevel { get; }
        
        bool CreateAdditionalExecutionSegment();

        bool NotifyAboutExecutionSegmentStopping(IExecutionSegment segment);
    }
}