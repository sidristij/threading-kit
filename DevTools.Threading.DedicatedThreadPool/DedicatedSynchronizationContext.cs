using System.Threading;
using DedicatedThreadPool.Jobs;

namespace DedicatedThreadPool
{
    public class DedicatedSynchronizationContext : SynchronizationContext
    {
        private readonly AdaptableThreadPool _threadPool;

        public DedicatedSynchronizationContext(AdaptableThreadPool threadPool)
        {
            _threadPool = threadPool;
        }

        public override void Post(SendOrPostCallback @delegate, object state)
        {
            _threadPool.Enqueue(@delegate, state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
        }
    }

    public class StaticExecutionThreadPoolStrategy
    {
        private ThreadWrapperBase _threadWrapper;
        
        internal void PlanToExecute(UnitOfWork unit)
        {
            _threadWrapper.OnThreadWakeUp();
            _threadWrapper.OnThreadGotWork();
        }
    }
}