using System;
using System.Collections.Generic;
using System.Threading;
using DedicatedThreadPool.Jobs;
using DevTools.Threading.Abstractions;

namespace DedicatedThreadPool
{
    public class AdaptableThreadPool : IThreadPool
    {
        private readonly Queue<UnitOfWork> _queue = new();
        
        public AdaptableThreadPool()
        {
            SynchronizationContext = new DedicatedSynchronizationContext(this);
        }

        public SynchronizationContext SynchronizationContext { get; }
        
        public void Enqueue(SendOrPostCallback action, object state)
        {
            _queue.Enqueue(new UnitOfWork(action, state));
        }

        public void RegisterWaitForSingleObject(WaitHandle handle, SendOrPostCallback action, object state = default, TimeSpan timeout = default)
        {
            var wfsoState = new WaitForSingleObjectState
            {
                Delegate = action,
                Timeout = timeout,
                Handle = handle,
                InternalState = state
            };
            
            _queue.Enqueue(new UnitOfWork(args =>
            {
                var workload = (WaitForSingleObjectState)args;
                workload.Handle.WaitOne(workload.Timeout);
                workload.Delegate(workload.InternalState);
            }, wfsoState));
        }

        private class WaitForSingleObjectState
        {
            public object InternalState;
            public WaitHandle Handle;
            public TimeSpan Timeout;
            public SendOrPostCallback Delegate;
        }
    }
}