namespace DevTools.Threading
{
    internal interface IThreadPoolInternalData
    {
        WorkStealingQueueList QueueList { get; }         
    }
}