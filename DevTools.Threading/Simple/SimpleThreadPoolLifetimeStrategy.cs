using System;
using System.Diagnostics;
using System.Threading;

namespace DevTools.Threading
{
    public class SimpleThreadPoolLifetimeStrategy : IThreadPoolLifetimeStrategy
    {
        private const int MinIntervalToStartWorkitem = 500; // ms
        private const int MinIntervalBetweenStops = 500; // ms
        private const int MinIntervalBetweenStarts = 200; // ms
        
        private readonly CyclicQueue _valuableIntervals = new();
        private readonly IThreadPoolThreadsManagement _threadsManagement;
        private volatile int _workitemsDoneFromLastStart = 0; 
        private Stopwatch LastStopBreakpoint = Stopwatch.StartNew();
        private Stopwatch LastStartBreakpoint = Stopwatch.StartNew();
        private object _threadCreationLock = new object();

        public SimpleThreadPoolLifetimeStrategy(IThreadPoolThreadsManagement threadsManagement)
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
            int globalQueueCount, int workitemsDone, double timeSpanMs)
        {
            if (LastStopBreakpoint.ElapsedMilliseconds > MinIntervalBetweenStops)
            {
                if(_threadsManagement.NotifyExecutionSegmentStopping(executionSegment))
                {
                    LastStopBreakpoint.Restart();
                    return true;
                }

                ;
            }
            return false;
        }

        /// <summary>
        /// Local strategy have some work and asks to help to parallelize it. We should increment threads count 1-by-1. 
        /// </summary>
        public void RequestForThreadStartIfNeed(int globalQueueCount, int workItemsDone, float timeSpanMs)
        {
            if (workItemsDone > 0)
            {
                _valuableIntervals.Add(timeSpanMs / workItemsDone);
            }

            // check if can start new thread
            if (LastStartBreakpoint.ElapsedMilliseconds > MinIntervalBetweenStarts)
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
                
                LastStartBreakpoint.Restart();
            }
        }
    }
}