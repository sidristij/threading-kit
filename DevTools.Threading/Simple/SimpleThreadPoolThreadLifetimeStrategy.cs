using System;

namespace DevTools.Threading
{
    public class SimpleThreadPoolThreadLifetimeStrategy : IThreadPoolThreadLifetimeStrategy
    {
        private long HasNoWorkUpperBoundThreshold = 200; // ms
        private readonly IExecutionSegment _segment;
        private readonly IThreadPoolLifetimeStrategy _poolStrategy;
        private long _hasWorkBreakpoint;

        public SimpleThreadPoolThreadLifetimeStrategy(
            IExecutionSegment segment,
            IThreadPoolLifetimeStrategy poolStrategy)
        {
            _hasWorkBreakpoint = Environment.TickCount;
            _segment = segment;
            _poolStrategy = poolStrategy;
        }
        
        public bool CheckCanContinueWork(int globalQueueCount, int workitemsDone, double timeSpanMs)
        {
            var immediateNothing = (timeSpanMs < 0) && workitemsDone == 0;
            var gotNothingOnLoop = timeSpanMs >= 0 && workitemsDone == 0;
            var currentBreakpoint = Environment.TickCount;
            
            // has work: just remember timestamp
            if (workitemsDone > 0)
            {
                _poolStrategy.RequestForThreadStartIfNeed(globalQueueCount, workitemsDone, timeSpanMs);
                _hasWorkBreakpoint = Environment.TickCount;
                return true;
            }

            // has no work: calculate time interval for this state
            // and if interval is too high, allow to stop thread (parent is spinning)
            if (currentBreakpoint - _hasWorkBreakpoint > HasNoWorkUpperBoundThreshold)
            {
                if (immediateNothing)
                {
                    // reset timer
                    _hasWorkBreakpoint = currentBreakpoint;
                    return _poolStrategy.RequestForThreadStop(_segment, globalQueueCount, workitemsDone, timeSpanMs) == false;
                }
            }
            
            return true;
        }
    }
}