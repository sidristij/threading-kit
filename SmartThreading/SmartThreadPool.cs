using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPool<TThreadPoolLogic, TThreadPoolStrategy, TPoolParameter> : 
        IThreadPool<TPoolParameter>, IThreadPoolThreadsManagement
        where TThreadPoolStrategy : IThreadPoolStrategy, new()
        where TThreadPoolLogic : ExecutionSegmentLogicBase, new() 
    {
        #region privates
        private const string ManagementSegmentName = "Management segment";
        private const string WorkingSegmentName = "Working segment";
        private readonly int MaxAllowedThreads;
        private readonly int MinAllowedThreads;
        
        private readonly SmartThreadPoolQueue _defaultQueue;
        private readonly SmartThreadPoolQueue[] _queues = new SmartThreadPoolQueue[Math.Abs((int)ThreadPoolItemPriority.High - (int)ThreadPoolItemPriority.Low)];
        
        private readonly ThreadWrappingQueue _managementSegment = new ThreadWrappingQueue(ManagementSegmentName);
        private readonly HashSet<ThreadWrappingQueue> _threads = new();
        private readonly ConcurrentQueue<ThreadWrappingQueue> _parkedSegments = new();
        private readonly HashSet<ThreadWrappingQueue> _frozenSegments = new();
        
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
            
            _managementSegment.SetExecutingUnit(() =>
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
        public int ParallelismLevel => _threads.Count;
        
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
        
        bool IThreadPoolThreadsManagement.CreateAdditionalExecutionSegments(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _managementSegment.SetExecutingUnit(() =>
                {
                    CreateAdditionalThreadImpl();
                });
            }
            return true;
        }

        bool IThreadPoolThreadsManagement.NotifyAboutExecutionSegmentStopping(ThreadWrappingQueue segment)
        {
            lock (_threads)
            {
                if (_threads.Count > MinAllowedThreads)
                {
                    _threads.Remove(segment);
                    _frozenSegments.Remove(segment);
                    _parkedSegments.Enqueue(segment);
                    return true;
                }
            }

            // disallow action
            return false;
        }

        private void CreateAdditionalThreadImpl()
        {
            ThreadWrappingQueue threadWrappingQueue = default;

            if (_parkedSegments.TryDequeue(out var parked))
            {
                _threads.Add(parked);
                threadWrappingQueue = parked;
            }
            else
            {
                lock (_threads)
                {
                    if (_threads.Count < MaxAllowedThreads)
                    {
                        var index = Interlocked.Increment(ref _threadsCounter);
                        threadWrappingQueue = new ThreadWrappingQueue($"{WorkingSegmentName}: #{index}");
                        _threads.Add(threadWrappingQueue);
                    }
                }
            }

            if (threadWrappingQueue != default)
            {
                var segmentLogic = new TThreadPoolLogic();
                var strategy = _globalStrategy.CreateThreadStrategy(threadWrappingQueue);
                segmentLogic.InitializeAndStart(this, _defaultQueue, strategy, threadWrappingQueue);
                MaxHistoricalParallelismLevel = Math.Max(MaxHistoricalParallelismLevel, ParallelismLevel);
            }
        }

        private void StartFrozenThreadsCheck()
        {
            // plan check to management thread
            _timer = new Timer(_ =>
            {
                _managementSegment.SetExecutingUnit(CheckFrozenAndAddThread);
            }, default, _timerInterval, _timerInterval);
        }

        private void CheckFrozenAndAddThread()
        {
            var frozenCounter = 0;
            if(_threads == null) return;
            foreach (var segment in _threads.ToArray())
            {
                if (segment.Logic.CheckFrozen())
                {
                    lock (_threads)
                    {
                        // - remove frozen from collection 
                        // - add it to frozen list
                        // - save it to active threads list (to be moved into _parked at end of life)
                        if (_threads.Remove(segment))
                        {
                            _frozenSegments.Add(segment);
                            segment.RequestThreadStop();
                            segment.SetExecutingUnit(() =>
                            {
                                _frozenSegments.Remove(segment);
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