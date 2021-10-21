namespace DevTools.Threading
{
    public class GenericThreadPool : SmartThreadPool<ThreadPoolLogic, ThreadPoolStrategy, string>
    {
        public GenericThreadPool(int minAllowedThreads = 1, int maxAllowedWorkingThreads = -1) : base(minAllowedThreads, maxAllowedWorkingThreads)
        {
        }
    }
}