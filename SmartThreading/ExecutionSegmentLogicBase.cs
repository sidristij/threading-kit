using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DevTools.Threading
{
    public abstract class ExecutionSegmentLogicBase
    {
        private IThreadPool _threadPool;
        private SmartThreadPoolQueue _globalQueue;
        private ThreadWrappingQueue _threadWrappingQueue;
        private IParallelismLocalStrategy _strategy;
        private ManualResetEvent _stoppedEvent;
        private long _lastBreakpoint_µs;

        private readonly long DispatchQuantum_µs = TimeUtils.ms_to_µs(50);
        private readonly long MaxTimeForDelegateRun_µs = TimeUtils.ms_to_µs(1500);
        
        internal void InitializeAndStart(
            IThreadPool threadPool,
            SmartThreadPoolQueue globalQueue,
            IParallelismLocalStrategy strategy,
            ThreadWrappingQueue threadWrappingQueue)
        {
            _threadPool = threadPool;
            _globalQueue = globalQueue;
            _threadWrappingQueue = threadWrappingQueue;
            _strategy = strategy;
            _stoppedEvent = new ManualResetEvent(false);
            _threadWrappingQueue.SetExecutingUnit(this, ThreadWorker);
            _lastBreakpoint_µs = TimeUtils.GetTimestamp_µs();
        }

        protected abstract void OnStarted();
        
        protected abstract Task OnRun(PoolActionUnit poolActionUnit);
        
        protected abstract void OnStopping();
        
        /// <summary>
        /// Checks that thread is blocked and delegate isn't responding for 1,5s
        /// </summary>
        /// <returns></returns>
        protected internal bool CheckFrozen()
        {
            return ((TimeUtils.GetTimestamp_µs() - _lastBreakpoint_µs) > MaxTimeForDelegateRun_µs) &&
                   (_threadWrappingQueue.GetThreadStatus() & ThreadState.WaitSleepJoin) == ThreadState.WaitSleepJoin;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ThreadWorker()
        {
            try
            {
                // Init thread locals
                MakeBasicInitialization();

                // Notify user code
                OnStarted();

                // work cycle
                var askedToRemoveThread = false;
                var spinner = new SpinWait();
                
                while (askedToRemoveThread == false)
                {
                    var hasWork = false;
                    _lastBreakpoint_µs = TimeUtils.GetTimestamp_µs();
                    
                    // >= 50ms to work
                    Dispatch(ref hasWork, ref askedToRemoveThread);
                    
                    if (!hasWork)
                    {
                        spinner.SpinOnce();
                    }
                }
                
                // Make stopping logic
                OnStopping();
                
                // cleanup thread locals
                ThreadLocals.instance = default;
            }
            finally
            {
                _stoppedEvent.Set();
                _stoppedEvent.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Dispatch(ref bool hasWork, ref bool askedToFinishThread)
        {
            var workQueue = _globalQueue;
            var tl = ThreadLocals.instance;
            var workCounter = 0;
            var elapsed_µs = -1L;
            
            PoolActionUnit actionUnitItem = default;
          
            //
            // Save the start time of internal loop
            var start_in_µs = TimeUtils.GetTimestamp_µs();

            //
            // Loop until our quantum expires or there is no work.
            while (askedToFinishThread == false)
            {
                if(workQueue.TryDequeue(ref actionUnitItem))
                {
                    hasWork = true;
                    OnRun(actionUnitItem);
                    workCounter++;
                }
                else
                {
                    // if there is no work, just ask to kill our thread
                    // and if not, spin and continue
                    break;
                }
                
                // Check if the dispatch quantum has expired
                elapsed_µs = TimeUtils.GetTimestamp_µs() - start_in_µs;
                if (elapsed_µs > DispatchQuantum_µs)
                {
                    break;
                }
            }
            
            //
            // Request for current thread stop or start additional thread
            if (_strategy.RequestForParallelismLevelChanged(_globalQueue.GlobalCount, workCounter, elapsed_µs) == ParallelismLevelChange.Decrease)
            {
                tl.TransferLocalWork();
                askedToFinishThread = true;
            }
        }
        
        private void MakeBasicInitialization()
        {
            // Assign access to my shared queue of local items
            var tl = ThreadLocals.instance;
            if (tl == null)
            {
                tl = new ThreadLocals(
                    _globalQueue, ((IThreadPoolInternals)_globalQueue).QueueList);
                ThreadLocals.instance = tl;
            }

            // Set current sync context
            SynchronizationContext.SetSynchronizationContext(_threadPool.SynchronizationContext);
        }
    }
}