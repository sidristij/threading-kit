namespace DevTools.Threading
{
    public class SimpleLogic : SimplifiedLogicBase
    {
        protected override void OnThreadStarted()
        {
            if (ThreadPoolWorkQueueThreadLocals.instance == null)
            {
                ThreadPoolWorkQueueThreadLocals.instance =
                    new ThreadPoolWorkQueueThreadLocals(
                        ThreadPoolQueue,
                        ((IWorkStealingQueueListProvider)ThreadPoolQueue).QueueList);
            }
        }

        protected override void OnWorkArrived()
        {
            // ;
        }
    }
}