using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPoolStrategy : IThreadPoolStrategy
    {
        private readonly long MinIntervalToStartWorkitem_µs = TimeConsts.ms_to_µs(500);
        private readonly long MinIntervalBetweenStops_µs =  TimeConsts.ms_to_µs(500);
        private readonly long MinIntervalBetweenStarts_µs = TimeConsts.ms_to_µs(200);

        private readonly CyclicTimeRangesQueue _valuableIntervals = new();
        private readonly IThreadPoolThreadsManagement _threadsManagement;
        private long LastStopBreakpoint_µs = TimeConsts.GetTimestamp_µs();
        private long LastStartBreakpoint_µs = TimeConsts.GetTimestamp_µs();
        private volatile int _locked = 0;
        private object _threadCreationLock = new();

        public SmartThreadPoolStrategy(IThreadPoolThreadsManagement threadsManagement)
        {
            _threadsManagement = threadsManagement;
        }
        
        /// <summary>
        /// Local strategy have some work and asks to help to parallelize it. We should increment threads count 1-by-1. 
        /// </summary>
        public ParallelismLevelChange RequestForThreadStart(int globalQueueCount, int workItemsDone, long range_µs)
        {
            if (workItemsDone > 0)
            {
                _valuableIntervals.Add(range_µs / workItemsDone);
            }
            else
            {
                return ParallelismLevelChange.NoChanges;
            }

            // check if can start new thread
            var elapsed_µs = TimeConsts.GetTimestamp_µs() - LastStartBreakpoint_µs;
            if (elapsed_µs > MinIntervalBetweenStarts_µs)
            {
                var avgWorkitemCost_µs = _valuableIntervals.GetAvg();
                var parallelism = _threadsManagement.ParallelismLevel;
                var workitemsPerThreadTheoretical = globalQueueCount / parallelism;
                var timeToExecute_µs = avgWorkitemCost_µs * workitemsPerThreadTheoretical;

                if (timeToExecute_µs > MinIntervalToStartWorkitem_µs)
                {
                    // only one thread can enter this section. Other threads will skip it 
                    if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                    {
                        Interlocked.Add(ref LastStartBreakpoint_µs, elapsed_µs);
                        _threadsManagement.CreateAdditionalExecutionSegment();
                        Interlocked.Exchange(ref _locked, 0);
                        return ParallelismLevelChange.Increased;
                    }
                }
            }

            return ParallelismLevelChange.NoChanges;
        }
        
        /// <summary>
        /// Local strategy asks to stop thread because it have no work. We dont need to stop immediately
        /// all these threads because in this case we will catch situation where all threads will stop in one moment.
        /// And after this moment we can get a lot of work with no threads to get this work. So, we will stop them 1-by-1. 
        /// </summary>
        public ParallelismLevelChange RequestForThreadStop(
            IExecutionSegment executionSegment,
            int globalQueueCount, int workItemsDone, long range_µs)
        {
            var elapsed_µs = TimeConsts.GetTimestamp_µs() - LastStopBreakpoint_µs;
            if (elapsed_µs > MinIntervalBetweenStops_µs)
            {
                if(_threadsManagement.NotifyAboutExecutionSegmentStopping(executionSegment))
                {
                    Interlocked.Add(ref LastStopBreakpoint_µs, elapsed_µs);
                    return ParallelismLevelChange.Decrease;
                }

                ;
            }
            return ParallelismLevelChange.NoChanges;
        }
    }
}