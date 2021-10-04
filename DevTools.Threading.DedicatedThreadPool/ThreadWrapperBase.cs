using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DedicatedThreadPool.Jobs;

namespace DedicatedThreadPool
{
    public abstract class ThreadWrapperBase
    {
        private readonly AdaptableThreadPool _threadPool;
        private readonly ManualResetEvent _stoppedEvent;
        private readonly Queue<UnitOfWork> _queue = new(128);
        private readonly CyclicQueue<long> _timings = new(8);
        private Thread _thread;

        public ThreadWrapperBase(AdaptableThreadPool threadPool)
        {
            _threadPool = threadPool;
            _stoppedEvent = new ManualResetEvent(false);
            _thread = new Thread(ThreadWorker);
        }

        public void ReInitialize()
        {
            
        }

        private void ThreadWorker()
        {
            var stopwatch = Stopwatch.StartNew();
            
            // ...
            SynchronizationContext.SetSynchronizationContext(_threadPool.SynchronizationContext);
            
            // ...
            OnThreadStarted();
            
            // work cycle
            while (true)
            {
                if (_queue.TryDequeue(out var item))
                {
                    stopwatch.Restart();
                    
                    item.Run();
                    
                    stopwatch.Stop();
                    _timings.Add(stopwatch.ElapsedMilliseconds);
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
        
        
        internal abstract void OnThreadGotWork();
        
        protected abstract void OnThreadPaused();
    }
}