namespace DevTools.Threading
{
    public class SimpleThreadPoolThreadLifetimeStrategy : IThreadPoolThreadLifetimeStrategy
    {
        public bool NotifyWorkItemComplete(int workitemsDone, int timeSpanMs)
        {
            var shouldStopThread = workitemsDone == 0 && (timeSpanMs > 20 || timeSpanMs == -1);
            return !shouldStopThread;
        }
    }
}