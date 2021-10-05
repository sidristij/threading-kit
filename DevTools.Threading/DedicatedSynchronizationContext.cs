using System.Threading;
using DevTools.Threading;

namespace DevTools.Threading
{
    /// <summary>
    /// Sync context used for assigning jobs to thread pool from async/awaits
    /// </summary>
    public class DedicatedSynchronizationContext : SynchronizationContext
    {
        private readonly IThreadPool _threadPool;

        public DedicatedSynchronizationContext(IThreadPool threadPool)
        {
            _threadPool = threadPool;
        }

        public override void Post(SendOrPostCallback @delegate, object state)
        {
            _threadPool.Enqueue(s => @delegate(s), state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            // ...
        }
    }
}