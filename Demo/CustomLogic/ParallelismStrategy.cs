using System;
using System.Threading;

namespace DevTools.Threading
{
    public class ParallelismStrategy : IParallelismStrategy
    {
        private readonly long MinIntervalToStartThread_µs = TimeUtils.ms_to_µs(100);
        private readonly long MinIntervalBetweenStops_µs =  TimeUtils.ms_to_µs(500);
        private readonly long MinIntervalBetweenStarts_µs = TimeUtils.ms_to_µs(100);
        private readonly long MinIntervalBetweenStartAnsStop_µs = TimeUtils.ms_to_µs(10_000);

        private readonly CyclicTimeRangesQueue _valuableIntervals = new();
        private IThreadPoolThreadsManagement _threadsManagement;
        private long LastStopBreakpoint_µs = TimeUtils.GetTimestamp_µs();
        private long LastStartBreakpoint_µs = TimeUtils.GetTimestamp_µs();
        private volatile int _locked;

        public void Initialize(IThreadPoolThreadsManagement threadsManagement)
        {
            _threadsManagement = threadsManagement;
        }

        /// <summary>
        /// Creates local strategy for given thread 
        /// </summary>
        public IParallelismLocalStrategy CreateLocalStrategy(ThreadWrappingQueue threadWrappingQueue)
        {
            return new ParallelismLocalStrategy(threadWrappingQueue, this);
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
            var elapsed_µs = TimeUtils.GetTimestamp_µs() - LastStartBreakpoint_µs;
            if (elapsed_µs > MinIntervalBetweenStarts_µs)
            {
                var avgWorkitemCost_µs = _valuableIntervals.GetAvg();
                var parallelism = _threadsManagement.ParallelismLevel;
                var workitemsPerThreadTheoretical = globalQueueCount / parallelism;
                var tailTimeTheoretical_µs = avgWorkitemCost_µs * workitemsPerThreadTheoretical;

                if (tailTimeTheoretical_µs > MinIntervalToStartThread_µs)
                {
                    // only one thread can enter this section. Other threads will skip it 
                    if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                    {
                        try
                        {
                            Interlocked.Add(ref LastStartBreakpoint_µs, elapsed_µs);
                            _threadsManagement.CreateThreadWrappingQueue(
                                Math.Max(1, (int)((tailTimeTheoretical_µs / MinIntervalToStartThread_µs - parallelism) / 2)));
                        }
                        finally
                        {
                            _locked = 0;
                        }
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
            ThreadWrappingQueue threadWrappingQueue,
            int globalQueueCount, int workItemsDone, long range_µs)
        {
            var currentBreakpoint_µs = TimeUtils.GetTimestamp_µs();
            var elapsedFromLastThreadStart_µs = currentBreakpoint_µs - LastStartBreakpoint_µs;
            var elapsedFromLastThreadStop_µs = currentBreakpoint_µs - LastStopBreakpoint_µs;
            
            if (elapsedFromLastThreadStart_µs > MinIntervalBetweenStartAnsStop_µs && 
                elapsedFromLastThreadStop_µs > MinIntervalBetweenStops_µs)
            {
                if (Interlocked.CompareExchange(ref _locked, 1, 0) == 0)
                {
                    if(_threadsManagement.NotifyThreadWrappingQueueStopping(threadWrappingQueue))
                    {
                        Interlocked.Add(ref LastStopBreakpoint_µs, elapsedFromLastThreadStop_µs);
                        _locked = 0;
                        return ParallelismLevelChange.Decrease;
                    }
                    _locked = 0;
                }
            }
            
            return ParallelismLevelChange.NoChanges;
        }
    }
}