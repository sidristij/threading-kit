namespace DedicatedThreadPool
{
    internal enum UnitOfWorkState
    {
        Waiting = 0,
        Running,
        Finished,
        Failed
    }
}