using System.Threading;
using DevTools.Threading.Abstractions;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private IThreadPoolQueue _globalQueue;
        private IExecutionSegment _executionSegment;
        private ManualResetEvent _stoppedEvent;

        public virtual void InitializeAndStart(
            IThreadPool threadPool,
            IThreadPoolQueue globalQueue,
            IExecutionSegment executionSegment)
        {
            _threadPool = threadPool;
            _globalQueue = globalQueue;
            _executionSegment = executionSegment;
            _stoppedEvent = new ManualResetEvent(false);
            _executionSegment.SetExecutingUnit(ThreadWorker);
        }

        protected IThreadPool ThreadPool => _threadPool;
        protected IThreadPoolQueue ThreadPoolQueue => _globalQueue;
        protected IExecutionSegment ExecutionSegment => _executionSegment;
        
        private void ThreadWorker(object ctx)
        {
            // ...
            SynchronizationContext.SetSynchronizationContext(_threadPool.SynchronizationContext);
            
            // ...
            OnThreadStarted();
            
            // work cycle
            while (true)
            {
                if (_globalQueue.TryDequeue(out var item))
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