namespace DevTools.Threading
{
    public interface IParallelismLocalStrategy
    {
        /// <summary>
        /// Should be called at unit of work execution end 
        /// </summary>
        /// <param name="globalQueueCount">Count of items in global work queue</param>
        /// <param name="jobsDone">Items done in <paramref name="range_µs"/> period</param>
        /// <param name="range_µs">Some period of work. -1 == didn't spin</param>
        ParallelismLevelChange RequestForParallelismLevelChanged(int globalQueueCount, int jobsDone, long range_µs);
    }

    public enum ParallelismLevelChange
    {
        Decrease,
        Increased,
        NoChanges
    }
}