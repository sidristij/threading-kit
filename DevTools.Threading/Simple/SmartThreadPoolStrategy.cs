using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPoolStrategy : IThreadPoolLifetimeStrategy
    {
        private readonly long MinIntervalToStartWorkitem_µs = TimeConsts.ms_to_µs(500);
        private readonly long MinIntervalBetweenStops_µs =  TimeConsts.ms_to_µs(500);
        private readonly long MinIntervalBetweenStarts_µs = TimeConsts.ms_to_µs(200);

        private readonly CyclicTimeRangesQueue _valuableIntervals = new();
        private readonly IThreadPoolThreadsManagement _threadsManagement;
        private volatile int _workitemsDoneFromLastStart = 0; 
        private long LastStopBreakpoint_µs = TimeConsts.GetTimestamp_µs();
        private long LastStartBreakpoint_µs = TimeConsts.GetTimestamp_µs();
        private object _threadCreationLock = new();

        public SmartThreadPoolStrategy(IThreadPoolThreadsManagement threadsManagement)
        {
            _threadsManagement = threadsManagement;
        }
        
        /// <summary>
        /// Local strategy asks to stop thread because it have no work. We dont need to stop immediately
        /// all these threads because in this case we will catch situation where all threads will stop in one moment.
        /// And after this moment we can get a lot of work with no threads to get this work. So, we will stop them 1-by-1. 
        /// </summary>
        public bool RequestForThreadStop(
            IExecutionSegment executionSegment,
            int globalQueueCount, int workItemsDone, long range_µs)
        {
            var elapsed_µs = TimeConsts.GetTimestamp_µs() - LastStopBreakpoint_µs;
            if (elapsed_µs > MinIntervalBetweenStops_µs)
            {
                if(_threadsManagement.NotifyExecutionSegmentStopping(executionSegment))
                {
                    Interlocked.Add(ref LastStopBreakpoint_µs, elapsed_µs);
                    return true;
                }

                ;
            }
            return false;
        }

        /// <summary>
        /// Local strategy have some work and asks to help to parallelize it. We should increment threads count 1-by-1. 
        /// </summary>
        public void RequestForThreadStartIfNeed(int globalQueueCount, int workItemsDone, long range_µs)
        {
            if (workItemsDone > 0)
            {
                _valuableIntervals.Add(range_µs / workItemsDone);
            }

            // check if can start new thread
            var elapsed_µs = TimeConsts.GetTimestamp_µs() - LastStartBreakpoint_µs;
            if (elapsed_µs > MinIntervalBetweenStarts_µs)
            {
                Interlocked.Add(ref _workitemsDoneFromLastStart, workItemsDone);
                
                var avgWorkitemCost_µs = _valuableIntervals.GetAvg();
                var parallelism = _threadsManagement.ParallelismLevel;
                var workitemsPerThreadTheoretical = globalQueueCount / parallelism;
                var timeToExecute_µs = avgWorkitemCost_µs * workitemsPerThreadTheoretical;

                if (timeToExecute_µs > MinIntervalToStartWorkitem_µs)
                {
                    var locked = Monitor.TryEnter(_threadCreationLock);
                    try
                    {
                        if (locked)
                        {
                            if (_threadsManagement.CreateAdditionalExecutionSegment())
                            {
                                Interlocked.Exchange(ref _workitemsDoneFromLastStart, 0);
                            }
                        }
                    }
                    finally
                    {
                        if (locked)
                        {
                            Monitor.Exit(_threadCreationLock);
                        }
                    }
                }
                
                Interlocked.Add(ref LastStartBreakpoint_µs, elapsed_µs);
            }
        }
    }
}