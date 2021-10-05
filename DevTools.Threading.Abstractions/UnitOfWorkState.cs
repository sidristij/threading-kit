namespace DevTools.Threading
{
    public enum UnitOfWorkState
    {
        Waiting = 0,
        Running,
        Finished,
        Failed
    }
}