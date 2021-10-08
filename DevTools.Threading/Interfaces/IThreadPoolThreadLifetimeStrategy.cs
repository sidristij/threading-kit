namespace DevTools.Threading
{
    public interface IThreadPoolThreadLifetimeStrategy
    {
        /// <summary>
        /// Should be called at unit of work execution end 
        /// </summary>
        /// <param name="globalQueueCount">Count of items in global work queue to understand volume</param>
        /// <param name="workItemsDone"></param>
        /// <param name="range_µs">some period of work. -1 == didn't spin</param>
        /// <param name="workitemsDone">items done in <paramref name="range_µs"/> period</param>
        /// <returns>should callee stop its thread or not</returns>
        bool CheckCanContinueWork(int globalQueueCount, int workItemsDone, long range_µs);
    }
}