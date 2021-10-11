namespace DevTools.Threading
{
    internal interface IThreadPoolInternalData
    {
        ThreadsLocalQueuesList QueueList { get; }         
    }
}