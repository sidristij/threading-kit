namespace DevTools.Threading
{
    internal interface IThreadPoolInternals
    {
        ThreadsLocalQueuesList QueueList { get; }         
    }
}