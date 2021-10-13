namespace DevTools.Threading
{
    public class SmartThreadPoolThreadStrategy : IThreadPoolThreadLifetimeStrategy
    {
        private long HasNoWorkUpperBoundThreshold_µs = TimeConsts.ms_to_µs(250);
        private readonly IExecutionSegment _segment;
        private readonly IThreadPoolLifetimeStrategy _poolStrategy;
        private long _hasWorkBreakpoint_µs;

        public SmartThreadPoolThreadStrategy(
            IExecutionSegment segment,
            IThreadPoolLifetimeStrategy poolStrategy)
        {
            _hasWorkBreakpoint_µs = TimeConsts.GetTimestamp_µs();
            _segment = segment;
            _poolStrategy = poolStrategy;
        }
        
        public bool CheckCanContinueWork(int globalQueueCount, int workitemsDone, long range_µs)
        {
            var immediateNothing = (range_µs < 0) && workitemsDone == 0;
            var currentBreakpoint_µs = TimeConsts.GetTimestamp_µs();
            
            // has work: just remember timestamp
            if (workitemsDone > 0)
            {
                _poolStrategy.RequestForThreadStartIfNeed(globalQueueCount, workitemsDone, range_µs);
                _hasWorkBreakpoint_µs = currentBreakpoint_µs;
                return true;
            }

            // has no work: calculate time interval for this state
            // and if interval is too high, allow to stop thread (parent is spinning)
            if (currentBreakpoint_µs - _hasWorkBreakpoint_µs > HasNoWorkUpperBoundThreshold_µs)
            {
                if (immediateNothing)
                {
                    // reset timer
                    _hasWorkBreakpoint_µs = currentBreakpoint_µs;
                    // ask for thread stop from global strategy
                    return _poolStrategy.RequestForThreadStop(_segment, globalQueueCount, workitemsDone, range_µs) == false;
                }
            }
            
            return true;
        }
    }
}