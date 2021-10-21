namespace DevTools.Threading
{
    internal class SmartThreadPoolThreadStrategy : IThreadPoolThreadStrategy
    {
        private readonly long HasNoWorkUpperBoundThreshold_µs = TimeConsts.ms_to_µs(250);
        private readonly ThreadWrapper _threadWrapper;
        private readonly IThreadPoolStrategy _poolStrategy;
        private long _lastBreakpoint_µs;

        public SmartThreadPoolThreadStrategy(
            ThreadWrapper threadWrapper,
            IThreadPoolStrategy poolStrategy)
        {
            _lastBreakpoint_µs = TimeConsts.GetTimestamp_µs();
            _threadWrapper = threadWrapper;
            _poolStrategy = poolStrategy;
        }

        /// <summary>
        /// Should be called at unit of work execution end 
        /// </summary>
        /// <param name="globalQueueCount">Count of items in global work queue to understand volume</param>
        /// <param name="range_µs">some period of work. -1 == didn't spin</param>
        /// <param name="jobsDone">items done in <paramref name="range_µs"/> period</param>
        /// <returns>should callee stop its thread or not</returns>
        public ParallelismLevelChange RequestForParallelismLevelChanged(int globalQueueCount, int jobsDone, long range_µs)
        {
            var currentBreakpoint_µs = TimeConsts.GetTimestamp_µs();
            
            // has work: just remember timestamp
            if (jobsDone > 0)
            {
                _lastBreakpoint_µs = currentBreakpoint_µs;
                
                // just ask: maybe need to do this
                return _poolStrategy.RequestForThreadStart(globalQueueCount, jobsDone, range_µs);
            }
            
            // special case: has no work items immediately after entering loop
            var immediateNothing = (range_µs < 0) && jobsDone == 0;

            // has no work: calculate time interval for this state
            // and if interval is too high, allow to stop thread (thread work loop is spinning)
            if (immediateNothing && (currentBreakpoint_µs - _lastBreakpoint_µs > HasNoWorkUpperBoundThreshold_µs))
            {
                // reset timer: if global strategy disallows us from stopping thread we should spin again
                _lastBreakpoint_µs = currentBreakpoint_µs;
                
                // ask for thread stop from global strategy
                return _poolStrategy.RequestForThreadStop(_threadWrapper, globalQueueCount, jobsDone, range_µs);
            }
            
            return ParallelismLevelChange.NoChanges;
        }
    }
}