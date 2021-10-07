using System;
using System.Diagnostics;
using System.Threading;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private IThreadPoolQueue _globalQueue;
        private IExecutionSegment _executionSegment;
        private IThreadPoolThreadLifetimeStrategy _lifetimeStrategy;
        private ManualResetEvent _stoppedEvent;

        private const uint DispatchQuantumMs = 30;
        
        public virtual void InitializeAndStart(
            IThreadPool threadPool,
            IThreadPoolQueue globalQueue,
            IThreadPoolThreadLifetimeStrategy lifetimeStrategy,
            IExecutionSegment executionSegment)
        {
            _threadPool = threadPool;
            _globalQueue = globalQueue;
            _executionSegment = executionSegment;
            _lifetimeStrategy = lifetimeStrategy;
            _stoppedEvent = new ManualResetEvent(false);
            _executionSegment.SetExecutingUnit(SegmentWorker);
        }

        protected IThreadPool ThreadPool => _threadPool;
        protected IThreadPoolQueue ThreadPoolQueue => _globalQueue;
        protected IExecutionSegment ExecutionSegment => _executionSegment;
        
        private void SegmentWorker(object ctx)
        {
            // Assign access to my shared queue of local items
            var tl = ThreadPoolWorkQueueThreadLocals.instance; 
            if (tl == null)
            {
                tl = new ThreadPoolWorkQueueThreadLocals(
                    ThreadPoolQueue,
                    ((IThreadPoolInternalData)ThreadPoolQueue).QueueList);
                ThreadPoolWorkQueueThreadLocals.instance = tl;
            }

            // Set current sync context
            SynchronizationContext.SetSynchronizationContext(_threadPool.SynchronizationContext);

            // Notify user code
            OnStarted();

            // work cycle
            var hasWork = false;
            var askedToRemoveThread = false;
            var spinner = new SpinWait();
            
            while (askedToRemoveThread == false)
            {
                // ~ 30ms to work
                Dispatch(ref hasWork, ref askedToRemoveThread);
                
                if (!hasWork)
                {
                    spinner.SpinOnce();
                }
            }
            
            // Make stopping logic
            OnStopping();

            _stoppedEvent.Set();
        }

        protected abstract void OnStarted();
        
        protected abstract void OnStopping();
        
        private void Dispatch(ref bool hasWork, ref bool askedToFinishThread)
        {
            var workQueue = _globalQueue;
            var tl = ThreadPoolWorkQueueThreadLocals.instance;
            int unitsOfWorkCounter = 0;

            UnitOfWork workItem;
            {
                var missedSteal = false;
                workItem = workQueue.Dequeue(ref missedSteal);

                if (workItem.InternalObjectIndex == 0)
                {
                    hasWork = false;
                    
                    if (_lifetimeStrategy.CheckCanContinueWork(_globalQueue.GlobalCount, 0, -1) == false)
                    {
                        tl.TransferLocalWork();
                        askedToFinishThread = true;
                    }
                    return;
                }
            }

            //
            // Save the start time
            //
            var sw = Stopwatch.StartNew();

            //
            // Loop until our quantum expires or there is no work.
            //
            while (true)
            {
                if (workItem.InternalObjectIndex == 0)
                {
                    bool missedSteal = false;
                    workItem = workQueue.Dequeue(ref missedSteal);

                    if (workItem.InternalObjectIndex == 0)
                    {
                        if (missedSteal)
                        {
                            hasWork = true;
                            break;
                        }
                        // hasWork == false, askedToRemoveThread == false
                        return;
                    }
                    hasWork = true;
                }

                workItem.Run();
                unitsOfWorkCounter++;
                workItem = default;

                // Check if the dispatch quantum has expired
                if (sw.Elapsed.TotalMilliseconds < DispatchQuantumMs)
                {
                    continue;
                }

                if (_lifetimeStrategy.CheckCanContinueWork(_globalQueue.GlobalCount, unitsOfWorkCounter, (float)sw.Elapsed.TotalMilliseconds) == false)
                {
                    tl.TransferLocalWork();
                    askedToFinishThread = true;
                }
                break;
            }

            // if nothing was done
            if (unitsOfWorkCounter == 0 && hasWork == false)
            {
                // _lifetimeStrategy.NotifyUnitOfWorkCycleFinished()
            }
        }
    }
}