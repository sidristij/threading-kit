using System.Threading;
using System.Threading.Tasks;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private SmartThreadPoolQueue _globalQueue;
        private IExecutionSegment _executionSegment;
        private IThreadPoolThreadStrategy _strategy;
        private ManualResetEvent _stoppedEvent;
        private long _lastBreakpoint_µs;

        private readonly long DispatchQuantum_µs = TimeConsts.ms_to_µs(50);
        private readonly long MaxTimeForDelegateRun_µs = TimeConsts.ms_to_µs(1500);
        
        internal void InitializeAndStart(
            IThreadPool threadPool,
            SmartThreadPoolQueue globalQueue,
            IThreadPoolThreadStrategy strategy,
            IExecutionSegment executionSegment)
        {
            _threadPool = threadPool;
            _globalQueue = globalQueue;
            _executionSegment = executionSegment;
            _strategy = strategy;
            _stoppedEvent = new ManualResetEvent(false);
            _executionSegment.SetExecutingUnit(this, SegmentWorker);
            _lastBreakpoint_µs = TimeConsts.GetTimestamp_µs();
        }

        private IThreadPoolQueue ThreadPoolQueue => _globalQueue;

        /// <summary>
        /// Checks that thread is blocked and delegate isn't responding for 1,5s
        /// </summary>
        /// <returns></returns>
        protected internal bool CheckFrozen()
        {
            return ((TimeConsts.GetTimestamp_µs() - _lastBreakpoint_µs) > MaxTimeForDelegateRun_µs) &&
                   (_executionSegment.GetThreadStatus() & ThreadState.WaitSleepJoin) == ThreadState.WaitSleepJoin;
        }
        
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
            var askedToRemoveThread = false;
            var spinner = new SpinWait();
            
            while (askedToRemoveThread == false)
            {
                var hasWork = false;
                _lastBreakpoint_µs = TimeConsts.GetTimestamp_µs();
                
                // >= 50ms to work
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
            unitOfWork.Run<object>(null);
        }
        
        private void Dispatch(ref bool hasWork, ref bool askedToFinishThread)
        {
            var workQueue = _globalQueue;
            var tl = ThreadLocals.instance;
            var workCounter = 0;
            var cycles = -1;
            var elapsed_µs = -1L;
            
            UnitOfWork workItem = default;
          
            //
            // Save the start time of internal loop
            var start_in_µs = TimeConsts.GetTimestamp_µs();

            //
            // Loop until our quantum expires or there is no work.
            while (askedToFinishThread == false)
            {
                // results 0 for first time
                cycles++;
                
                if(workQueue.TryDequeue(ref workItem))
                {
                    hasWork = true;
                    OnRun(workItem);
                    workCounter++;
                }
                else
                {
                    // if there is no work, just ask to kill our thread
                    // and if not, spin and continue
                    break;
                }
                
                // Check if the dispatch quantum has expired
                elapsed_µs = TimeConsts.GetTimestamp_µs() - start_in_µs;
                if (elapsed_µs > DispatchQuantum_µs)
                {
                    break;
                }
            }
            
            if (_strategy.RequestForParallelismLevelChanged(_globalQueue.GlobalCount, workCounter, elapsed_µs) == ParallelismLevelChange.Decrease)
            {
                tl.TransferLocalWork();
                askedToFinishThread = true;
            }
        }
    }
}