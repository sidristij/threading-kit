namespace DevTools.Threading
{
    public class GenericThreadPool : SmartThreadPool<ThreadLifetimeLogic, ParallelismStrategy, string>
    {
        public GenericThreadPool(int minAllowedThreads = 1, int maxAllowedWorkingThreads = -1) : base(minAllowedThreads, maxAllowedWorkingThreads)
        {
        }
    }
}