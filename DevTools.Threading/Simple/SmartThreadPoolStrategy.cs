using System.Diagnostics;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPoolStrategy : IThreadPoolLifetimeStrategy
    {
        private readonly long MinIntervalToStartWorkitem = (500 * Time.ticks_to_ms) / Time.ticks_to_µs; // ms
        private readonly long MinIntervalBetweenStops =  (500 * Time.ticks_to_ms) / Time.ticks_to_µs; // ms
        private readonly long MinIntervalBetweenStarts =  (200 * Time.ticks_to_ms) / Time.ticks_to_µs; // ms

        private readonly CyclicQueue _valuableIntervals = new();
        private readonly IThreadPoolThreadsManagement _threadsManagement;
        private volatile int _workitemsDoneFromLastStart = 0; 
        private long LastStopBreakpoint = Stopwatch.GetTimestamp() / Time.ticks_to_µs;
        private long LastStartBreakpoint = Stopwatch.GetTimestamp() / Time.ticks_to_µs;
        private object _threadCreationLock = new object();

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
            var elapsed = Stopwatch.GetTimestamp() / Time.ticks_to_µs - LastStopBreakpoint;
            if (elapsed > MinIntervalBetweenStops)
            {
                if(_threadsManagement.NotifyExecutionSegmentStopping(executionSegment))
                {
                    Interlocked.Add(ref LastStopBreakpoint, elapsed);
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
            var elapsed = Stopwatch.GetTimestamp() / Time.ticks_to_µs - LastStartBreakpoint;
            if (elapsed > MinIntervalBetweenStarts)
            {
                Interlocked.Add(ref _workitemsDoneFromLastStart, workItemsDone);
                
                var avgWorkitemCost = _valuableIntervals.GetAvg();
                var parallelism = _threadsManagement.ParallelismLevel;
                var workitemsPerThreadTheoretical = globalQueueCount / parallelism;
                var timeToExecute = avgWorkitemCost * workitemsPerThreadTheoretical;

                if (timeToExecute > MinIntervalToStartWorkitem)
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
                
                Interlocked.Add(ref LastStartBreakpoint, elapsed);
            }
        }
    }
}