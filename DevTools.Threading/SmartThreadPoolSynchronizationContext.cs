using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevTools.Threading.Exceptions;

namespace DevTools.Threading
{
    /// <summary>
    /// Sync context used for assigning jobs to thread pool from async/awaits
    /// </summary>
    public class SmartThreadPoolSynchronizationContext : SynchronizationContext
    {
        private readonly IThreadPool _threadPool;

        public SmartThreadPoolSynchronizationContext(IThreadPool threadPool)
        {
            _threadPool = threadPool;
        }

        public override void Post(SendOrPostCallback @delegate, object state)
        {
            _threadPool.Enqueue(s => @delegate(s), state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new ThreadPoolException($"{_threadPool.GetType().FullName} not supporting synchronous calls");
        }
    }

    public class SmartThreadPoolTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Array.Empty<Task>();
        }

        protected override void QueueTask(Task task)
        {
            FromCurrentSynchronizationContext();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            throw new System.NotImplementedException();
        }
    }
}