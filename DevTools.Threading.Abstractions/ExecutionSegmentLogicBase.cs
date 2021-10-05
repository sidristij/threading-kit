using System.Threading;
using DevTools.Threading.Abstractions;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private IThreadPoolQueue _queue;
        private IExecutionSegment _executionSegment;
        private ManualResetEvent _stoppedEvent;

        public virtual void InitializeAndStart(
            IThreadPool threadPool,
            IThreadPoolQueue queue,
            IExecutionSegment executionSegment)
        {
            _threadPool = threadPool;
            _queue = queue;
            _executionSegment = executionSegment;
            _stoppedEvent = new ManualResetEvent(false);
            _executionSegment.SetExecutingUnit(ThreadWorker);
        }

        private void ThreadWorker(object ctx)
        {
            // ...
            SynchronizationContext.SetSynchronizationContext(_threadPool.SynchronizationContext);
            
            // ...
            OnThreadStarted();
            
            // work cycle
            while (true)
            {
                if (_queue.TryDequeue(out var item))
                {
                    item.Run();
                }
            }
            
            // Make stopping logic
            OnThreadStopping();

            _stoppedEvent.Set();
        }

        /// <summary>
        /// Called when next event will be Got Work
        /// </summary>
        internal void OnThreadWakeUp()
        {
            
        }

        protected abstract void OnThreadStarted();
        
        protected abstract void OnThreadStopping();
        
        protected abstract void OnWorkArrived();
        
        protected abstract void OnThreadPaused();
    }
}