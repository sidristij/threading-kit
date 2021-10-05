namespace DevTools.Threading
{
    internal interface IWorkStealingQueueListProvider
    {
        WorkStealingQueueList QueueList { get; }         
    }
}