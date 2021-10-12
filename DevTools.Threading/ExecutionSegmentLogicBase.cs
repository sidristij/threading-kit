using System.Diagnostics;
using System.Threading;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private SmartThreadPoolQueue _globalQueue;
        private IExecutionSegment _executionSegment;
        private IThreadPoolThreadLifetimeStrategy _lifetimeStrategy;
        private ManualResetEvent _stoppedEvent;

        private readonly long DispatchQuantum_µs = (30 * Time.ticks_to_ms) / Time.ticks_to_µs;
        
        internal void InitializeAndStart(
            IThreadPool threadPool,
            SmartThreadPoolQueue globalQueue,
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

        private IThreadPoolQueue ThreadPoolQueue => _globalQueue;
        
        private void SegmentWorker(object ctx)
        {
            // Assign access to my shared queue of local items
            var tl = ThreadLocals.instance; 
            if (tl == null)
            {
                tl = new ThreadLocals(
                    ThreadPoolQueue,
                    ((IThreadPoolInternalData)ThreadPoolQueue).QueueList);
                ThreadLocals.instance = tl;
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
            
            ThreadLocals.instance = default;
            _stoppedEvent.Set();
            _stoppedEvent.Dispose();
        }

        protected abstract void OnStarted();
        
        protected abstract void OnStopping();

        protected virtual void OnRun(UnitOfWork unitOfWork)
        {
            unitOfWork.Run();
        }
        
        private void Dispatch(ref bool hasWork, ref bool askedToFinishThread)
        {
            var workQueue = _globalQueue;
            var tl = ThreadLocals.instance;
            var unitsOfWorkCounter = 0;
            var missedSteal = false;

            UnitOfWork workItem = default;
          
            //
            // Save the start time
            var start_in_µs = Stopwatch.GetTimestamp() / Time.ticks_to_µs;

            //
            // Loop until our quantum expires or there is no work.
            while (askedToFinishThread == false)
            {
                workQueue.Dequeue(ref workItem);
                if (workItem.InternalObjectIndex == 0)
                {
                    break;
                }
                
                hasWork = true;
                OnRun(workItem);
                unitsOfWorkCounter++;
                workItem = default;

                // Check if the dispatch quantum has expired
                var elapsed_µs = (Stopwatch.GetTimestamp() >> Time.ticks_to_µs_shift) - start_in_µs;
                if (elapsed_µs < DispatchQuantum_µs)
                {
                    continue;
                }

                if (_lifetimeStrategy.CheckCanContinueWork(_globalQueue.GlobalCount, unitsOfWorkCounter, elapsed_µs) == false)
                {
                    tl.TransferLocalWork();
                    askedToFinishThread = true;
                }
            }
        }
    }
}