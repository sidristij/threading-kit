using System;
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
            _executionSegment.SetExecutingUnit(ThreadWorker);
        }

        protected IThreadPool ThreadPool => _threadPool;
        protected IThreadPoolQueue ThreadPoolQueue => _globalQueue;
        protected IExecutionSegment ExecutionSegment => _executionSegment;
        
        private void ThreadWorker(object ctx)
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
            OnThreadStarted();

            // work cycle
            var finishRequested = false;
            var idleLoopsCount = 0;
            while (finishRequested == false)
            {
                var hasWork = false;
                var askedToRemoveThread = false;
                
                // ~ 30ms to work
                Dispatch(ref hasWork, ref askedToRemoveThread);
                
                if (askedToRemoveThread)
                {
                    break;
                }

                if (!hasWork)
                {
                    if (idleLoopsCount > 12)
                    {
                        idleLoopsCount = 0;
                        
                    } else if (idleLoopsCount > 10)
                    {
                        Thread.Yield();
                    }
                }
                else
                {
                    idleLoopsCount = 0;
                }

                idleLoopsCount++;
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

        private void Dispatch(ref bool hasWork, ref bool askedToRemoveThread)
        {
            var workQueue = _globalQueue;
            var tl = ThreadPoolWorkQueueThreadLocals.instance;
            int unitsOfWorkCounter = 0;

            // Before dequeuing the first work item, acknowledge that the thread request has been satisfied
            // workQueue.MarkThreadRequestSatisfied();

            UnitOfWork workItem;
            {
                var missedSteal = false;
                workItem = workQueue.Dequeue(ref missedSteal);

                if (workItem == null)
                {
                    //
                    // No work.
                    // If we missed a steal, though, there may be more work in the queue.
                    // Instead of looping around and trying again, we'll just request another thread.  Hopefully the thread
                    // that owns the contended work-stealing queue will pick up its own workitems in the meantime,
                    // which will be more efficient than this thread doing it anyway.
                    //
                    if (missedSteal)
                    {
                        // workQueue.EnsureThreadRequested();
                    }

                    // Tell the VM we're returning normally, not because Hill Climbing asked us to return.
                    hasWork = false;
                    return;
                }

                // A work item was successfully dequeued, and there may be more work items to process. Request a thread to
                // parallelize processing of work items, before processing more work items. Following this, it is the
                // responsibility of the new thread and other enqueuers to request more threads as necessary. The
                // parallelization may be necessary here for correctness (aside from perf) if the work item blocks for some
                // reason that may have a dependency on other queued work items.
                // workQueue.EnsureThreadRequested();
            }


            var currentThread = tl.currentThread;

            //
            // Save the start time
            //
            int startTickCount = Environment.TickCount;

            //
            // Loop until our quantum expires or there is no work.
            //
            bool returnValue;
            while (true)
            {
                if (workItem == null)
                {
                    bool missedSteal = false;
                    workItem = workQueue.Dequeue(ref missedSteal);

                    if (workItem == null)
                    {
                        //
                        // No work.
                        // If we missed a steal, though, there may be more work in the queue.
                        // Instead of looping around and trying again, we'll just request another thread.  Hopefully the thread
                        // that owns the contended work-stealing queue will pick up its own workitems in the meantime,
                        // which will be more efficient than this thread doing it anyway.
                        //
                        if (missedSteal)
                        {
                            // Ensure a thread is requested before returning
                            hasWork = true;
                            break;
                        }

                        // Tell the VM we're returning normally, not because Hill Climbing asked us to return.
                        return;
                    }

                    hasWork = true;
                }

                //
                // Execute the workitem outside of any finally blocks, so that it can be aborted if needed.
                //
                workItem.Run();
                unitsOfWorkCounter++;
                // Release refs
                workItem = null;

                //
                // Notify the VM that we executed this workitem.  This is also our opportunity to ask whether Hill Climbing wants
                // us to return the thread to the pool or not.
                //
                int currentTickCount = Environment.TickCount;
                if (!_lifetimeStrategy.NotifyWorkItemComplete(unitsOfWorkCounter, currentTickCount - startTickCount))
                {
                    // This thread is being parked and may remain inactive for a while. Transfer any thread-local work items
                    // to ensure that they would not be heavily delayed.
                    tl.TransferLocalWork();

                    // Ensure a thread is requested before returning
                    askedToRemoveThread = true;
                    break;
                }

                // Check if the dispatch quantum has expired
                if ((uint)(currentTickCount - startTickCount) < DispatchQuantumMs)
                {
                    continue;
                }

                // This method will continue to dispatch work items. Refresh the start tick count for the next dispatch
                // quantum and do some periodic activities.
                startTickCount = currentTickCount;
            }

            // workQueue.EnsureThreadRequested();
        }
    }
}