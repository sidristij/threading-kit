using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPool<TPoolParameter> : IThreadPool<TPoolParameter>, IThreadPoolThreadsManagement
    {
        private const string ManagementSegmentName = "Management segment";
        private const string WorkingSegmentName = "Working segment";
        private readonly int MaxAllowedThreads;
        private readonly int MinAllowedThreads;
        
        private readonly SmartThreadPoolQueue _globalQueue;
        private readonly SmartThreadPoolQueue[] _queues = new SmartThreadPoolQueue[Math.Abs((int)ThreadPoolItemPriority.High - (int)ThreadPoolItemPriority.Low)];
        
        private readonly IExecutionSegment _managementSegment = new ExecutionSegment(ManagementSegmentName);
        private readonly HashSet<IExecutionSegment> _segments = new();
        private readonly ConcurrentQueue<IExecutionSegment> _parkedSegments = new();
        private readonly HashSet<IExecutionSegment> _frozenSegments = new();
        
        private readonly ManualResetEvent _event = new(false);
        private readonly SmartThreadPoolStrategy _globalStrategy;
        private Timer _timer;
        private readonly TimeSpan _timerInterval = TimeSpan.FromSeconds(1);
        private volatile int _threadsCounter = 0;
        
        public SmartThreadPool(int minAllowedThreads = 1, int maxAllowedWorkingThreads = -1)
        {
            MinAllowedThreads = minAllowedThreads;
            MaxAllowedThreads = maxAllowedWorkingThreads > 0 ? maxAllowedWorkingThreads : Environment.ProcessorCount * 2;
            SynchronizationContext = new SmartThreadPoolSynchronizationContext(this);
            MaxHistoricalParallelismLevel = 0;
            
            for (int i = 0; i < _queues.Length; i++)
            {
                _queues[i] = new SmartThreadPoolQueue();
            }

            _globalQueue = _queues[(int)ThreadPoolItemPriority.Default];
            
            _globalStrategy = new SmartThreadPoolStrategy(this);
            _managementSegment.SetExecutingUnit(_ =>
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
        public int ParallelismLevel => _segments.Count;
        
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
        public void Enqueue(ExecutionUnit unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with regular function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<object, void> unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized delegate 
        /// </summary>
        public void Enqueue(ExecutionUnit<TPoolParameter> unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with non-parametrized async delegate 
        /// </summary>
        public void Enqueue(ExecutionUnitAsync unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized async delegate 
        /// </summary>
        public void Enqueue(ExecutionUnitAsync<TPoolParameter> unit, object outer = default, bool preferLocal = true)
        {
            PoolWork poolWork = default;
            poolWork.Init(unit, outer);
            _globalQueue.Enqueue(poolWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with WaitHandle and non-parametrized continuation 
        /// </summary>
        public void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object outer = default, TimeSpan timeout = default)
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

            var poolWork = default(PoolWork);
            poolWork.Init(ExecutionUnitCallback, wfsoState);
            _globalQueue.Enqueue(poolWork, false);
        }
        
        bool IThreadPoolThreadsManagement.CreateAdditionalExecutionSegments(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _managementSegment.SetExecutingUnit(_ =>
                {
                    CreateAdditionalThreadImpl();
                });
            }
            return true;
        }

        bool IThreadPoolThreadsManagement.NotifyAboutExecutionSegmentStopping(IExecutionSegment segment)
        {
            lock (_segments)
            {
                if (_segments.Count > MinAllowedThreads)
                {
                    _segments.Remove(segment);
                    _frozenSegments.Remove(segment);
                    _parkedSegments.Enqueue(segment);
                    return true;
                }
            }

            // disallow action
            return false;
        }

        private bool CreateAdditionalThreadImpl()
        {
            IExecutionSegment threadSegment = default;

            if (_parkedSegments.TryDequeue(out var parked))
            {
                _segments.Add(parked);
                threadSegment = parked;
            }
            else
            {
                lock (_segments)
                {
                    if (_segments.Count < MaxAllowedThreads)
                    {
                        var index = Interlocked.Increment(ref _threadsCounter);
                        threadSegment = new ExecutionSegment($"{WorkingSegmentName}: #{index}");
                        _segments.Add(threadSegment);
                    }
                }
            }

            if (threadSegment != default)
            {
                var segmentLogic = new SmartThreadPoolLogic();
                var strategy = new SmartThreadPoolThreadStrategy(threadSegment, _globalStrategy);
                segmentLogic.InitializeAndStart(this, _globalQueue, strategy, threadSegment);
                MaxHistoricalParallelismLevel = Math.Max(MaxHistoricalParallelismLevel, ParallelismLevel);
                return true;
            }

            return false;
        }

        private void StartFrozenThreadsCheck()
        {
            // plan check to management thread
            _timer = new Timer(_ =>
            {
                _managementSegment.SetExecutingUnit(_ => { CheckFrozenAndAddThread(); });
            }, default, _timerInterval, _timerInterval);
        }

        private void CheckFrozenAndAddThread()
        {
            var frozenCounter = 0;
            if(_segments == null) return;
            foreach (var segment in _segments.ToArray())
            {
                if (segment.Logic.CheckFrozen())
                {
                    lock (_segments)
                    {
                        // - remove frozen from collection 
                        // - add it to frozen list
                        // - save it to active threads list (to be moved into _parked at end of life)
                        if (_segments.Remove(segment))
                        {
                            _frozenSegments.Add(segment);
                            segment.RequestThreadStop();
                            segment.SetExecutingUnit(_ =>
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
            public ExecutionUnit Delegate;
        }
    }
}