namespace DevTools.Threading
{
    public interface IThreadPoolThreadLifetimeStrategy
    {
        bool NotifyWorkItemComplete(int workitemsDone, int timeSpanMs);
    }
}