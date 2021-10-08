using System.Diagnostics;

namespace DevTools.Threading
{
    public class SimpleThreadPoolThreadLifetimeStrategy : IThreadPoolThreadLifetimeStrategy
    {
        private long HasNoWorkUpperBoundThreshold = (200 * Time.ticks_to_ms) / Time.ticks_to_µs; // ms
        private readonly IExecutionSegment _segment;
        private readonly IThreadPoolLifetimeStrategy _poolStrategy;
        private long _hasWorkBreakpoint;

        public SimpleThreadPoolThreadLifetimeStrategy(
            IExecutionSegment segment,
            IThreadPoolLifetimeStrategy poolStrategy)
        {
            _hasWorkBreakpoint = Stopwatch.GetTimestamp() / Time.ticks_to_µs;
            _segment = segment;
            _poolStrategy = poolStrategy;
        }
        
        public bool CheckCanContinueWork(int globalQueueCount, int workitemsDone, long range_µs)
        {
            var immediateNothing = (range_µs < 0) && workitemsDone == 0;
            var gotNothingOnLoop = range_µs >= 0 && workitemsDone == 0;
            var currentBreakpoint = Stopwatch.GetTimestamp() / Time.ticks_to_µs;
            
            // has work: just remember timestamp
            if (workitemsDone > 0)
            {
                _poolStrategy.RequestForThreadStartIfNeed(globalQueueCount, workitemsDone, range_µs);
                _hasWorkBreakpoint = currentBreakpoint;
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
                    return _poolStrategy.RequestForThreadStop(_segment, globalQueueCount, workitemsDone, range_µs) == false;
                }
            }
            
            return true;
        }
    }
}