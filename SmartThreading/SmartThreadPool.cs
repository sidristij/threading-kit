using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPool<TThreadPoolLogic, TThreadPoolStrategy, TPoolParameter> : 
        IThreadPool<TPoolParameter>, IThreadPoolThreadsManagement
        where TThreadPoolStrategy : IParallelismStrategy, new()
        where TThreadPoolLogic : ExecutionSegmentLogicBase, new() 
    {
        #region privates
        private const string ManagementThreadName = "Management thread";
        private const string WorkingThreadName = "Working thread";
        private readonly int MaxAllowedThreads;
        private readonly int MinAllowedThreads;
        
        private readonly SmartThreadPoolQueue _defaultQueue;
        private readonly SmartThreadPoolQueue[] _queues = new SmartThreadPoolQueue[Math.Abs((int)ThreadPoolItemPriority.High - (int)ThreadPoolItemPriority.Low)];
        
        private readonly ThreadWrappingQueue _managementQueue = new ThreadWrappingQueue(ManagementThreadName);
        private readonly HashSet<ThreadWrappingQueue> _activeThreads = new();
        private readonly HashSet<ThreadWrappingQueue> _frozenThreads = new();
        private readonly ConcurrentQueue<ThreadWrappingQueue> _parkedThreads = new();
        
        private readonly ManualResetEvent _event = new(false);
        private readonly TThreadPoolStrategy _globalStrategy;
        private Timer _timer;
        private readonly TimeSpan _timerInterval = TimeSpan.FromSeconds(1);
        private volatile int _threadsCounter;
        #endregion
            
        public SmartThreadPool(int minAllowedThreads = 1, int maxAllowedWorkingThreads = -1)
        {
            MinAllowedThreads = minAllowedThreads;
            MaxAllowedThreads = maxAllowedWorkingThreads > 0 ? maxAllowedWorkingThreads : Environment.ProcessorCount * 2;
            SynchronizationContext = new ThreadPoolSynchronizationContext(this);
            MaxHistoricalParallelismLevel = 0;
            
            for (int i = 0; i < _queues.Length; i++)
            {
                _queues[i] = new SmartThreadPoolQueue();
            }

            _defaultQueue = _queues[(int)ThreadPoolItemPriority.Default];
            
            _globalStrategy = new TThreadPoolStrategy();
            _globalStrategy.Initialize(this);
            
            _managementQueue.SetExecutingUnit(() =>
            {
                for (var i = 0; i < MinAllowedThreads; i++)
                {
                    CreateAdditionalThreadImpl();
                }
                _event.Set();
            });

            StartFrozenThreadsCheck();
        }

        /// <summary>
        /// Gets public synchronization context of pool threads which can be used
        /// as alternative way of delegates planning 
        /// </summary>
        public SynchronizationContext SynchronizationContext { get; }

        /// <summary>
        /// Gets active level of parallelism excluding parked or frozen threads
        /// </summary>
        public int ParallelismLevel => _activeThreads.Count;
        
        /// <summary>
        /// Gets maximum level of parallelism which pool got while its work
        /// </summary>
        public int MaxHistoricalParallelismLevel { get; private set; }

        /// <summary>
        /// Gets awaitable handle if client code wants to wait until all threads
        /// of pool started at pool startup
        /// </summary>
        public WaitHandle InitializedWaitHandle => _event;
     
        /// <summary>
        /// Initialize with regular delegate 
        /// </summary>
        public void Enqueue(PoolAction unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with regular function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<object, void> unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized delegate 
        /// </summary>
        public void Enqueue(PoolAction<TPoolParameter> unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with non-parametrized async delegate 
        /// </summary>
        public void Enqueue(PoolActionAsync unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized async delegate 
        /// </summary>
        public void Enqueue(PoolActionAsync<TPoolParameter> unit, object outer = default, bool preferLocal = true)
        {
            PoolActionUnit poolActionUnit = default;
            poolActionUnit.Init(unit, outer);
            _defaultQueue.Enqueue(poolActionUnit, preferLocal);
        }
        
        /// <summary>
        /// Initialize with WaitHandle and non-parametrized continuation 
        /// </summary>
        public void RegisterWaitForSingleObject(WaitHandle handle, PoolAction unit, object outer = default, TimeSpan timeout = default)
        {
            var wfsoState = new WaitForSingleObjectState
            {
                Delegate = unit,
                Timeout = timeout,
                Handle = handle,
                InternalState = outer
            };

            void ExecutionUnitCallback(object args)
            {
                var workload = (WaitForSingleObjectState)args;
                workload.Handle.WaitOne(workload.Timeout);
                workload.Delegate(workload.InternalState);
            }

            var poolWork = default(PoolActionUnit);
            poolWork.Init(ExecutionUnitCallback, wfsoState);
            _defaultQueue.Enqueue(poolWork, false);
        }
        
        bool IThreadPoolThreadsManagement.CreateThreadWrappingQueue(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _managementQueue.SetExecutingUnit(() =>
                {
                    CreateAdditionalThreadImpl();
                });
            }
            return true;
        }

        bool IThreadPoolThreadsManagement.NotifyThreadWrappingQueueStopping(ThreadWrappingQueue queue)
        {
            lock (_activeThreads)
            {
                if (_activeThreads.Count > MinAllowedThreads)
                {
                    _activeThreads.Remove(queue);
                    _frozenThreads.Remove(queue);
                    _parkedThreads.Enqueue(queue);
                    return true;
                }
            }

            // disallow action
            return false;
        }

        private void CreateAdditionalThreadImpl()
        {
            ThreadWrappingQueue threadWrappingQueue = default;

            if (_parkedThreads.TryDequeue(out var parked))
            {
                _activeThreads.Add(parked);
                threadWrappingQueue = parked;
            }
            else
            {
                lock (_activeThreads)
                {
                    if (_activeThreads.Count < MaxAllowedThreads)
                    {
                        var index = Interlocked.Increment(ref _threadsCounter);
                        threadWrappingQueue = new ThreadWrappingQueue($"{WorkingThreadName}: #{index}");
                        _activeThreads.Add(threadWrappingQueue);
                    }
                }
            }

            if (threadWrappingQueue != default)
            {
                var lifetimeLogic = new TThreadPoolLogic();
                var strategy = _globalStrategy.CreateLocalStrategy(threadWrappingQueue);
                lifetimeLogic.InitializeAndStart(this, _defaultQueue, strategy, threadWrappingQueue);
                MaxHistoricalParallelismLevel = Math.Max(MaxHistoricalParallelismLevel, ParallelismLevel);
            }
        }

        private void StartFrozenThreadsCheck()
        {
            // plan check to management thread
            _timer = new Timer(_ =>
            {
                _managementQueue.SetExecutingUnit(CheckFrozenAndAddThread);
            }, default, _timerInterval, _timerInterval);
        }

        private void CheckFrozenAndAddThread()
        {
            var frozenCounter = 0;
            if(_activeThreads == null) return;
            foreach (var wrappingQueue in _activeThreads)
            {
                if (wrappingQueue.Logic.CheckFrozen())
                {
                    lock (_activeThreads)
                    {
                        // - remove frozen from collection 
                        // - add it to frozen list
                        // - save it to active threads list (to be moved into _parked at end of life)
                        if (_activeThreads.Remove(wrappingQueue))
                        {
                            _frozenThreads.Add(wrappingQueue);
                            wrappingQueue.RequestThreadStop();
                            wrappingQueue.SetExecutingUnit(() =>
                            {
                                _frozenThreads.Remove(wrappingQueue);
                            });
                            frozenCounter++;
                        }
                    }

                    for (var i = 0; i < frozenCounter; i++)
                    {
                        CreateAdditionalThreadImpl();
                    }
                }
            }
        }
        
        private class WaitForSingleObjectState
        {
            public object InternalState;
            public WaitHandle Handle;
            public TimeSpan Timeout;
            public PoolAction Delegate;
        }
    }
}