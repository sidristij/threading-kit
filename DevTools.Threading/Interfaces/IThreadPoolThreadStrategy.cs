namespace DevTools.Threading
{
    public interface IThreadPoolThreadStrategy
    {
        /// <summary>
        /// Should be called at unit of work execution end 
        /// </summary>
        /// <param name="globalQueueCount">Count of items in global work queue to understand volume</param>
        /// <param name="jobsDone"></param>
        /// <param name="range_µs">some period of work. -1 == didn't spin</param>
        /// <param name="workitemsDone">items done in <paramref name="range_µs"/> period</param>
        /// <returns>should callee stop its thread or not</returns>
        ParallelismLevelChange RequestForParallelismLevelChanged(int globalQueueCount, int jobsDone, long range_µs);
    }

    public enum ParallelismLevelChange
    {
        Decrease,
        Increased,
        NoChanges
    }
}