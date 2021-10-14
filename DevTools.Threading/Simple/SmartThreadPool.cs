using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPool<TPoolParameter> : IThreadPool<TPoolParameter>, IThreadPoolThreadsManagement
    {
        private const string ManagementSegmentName = "Management segment";
        private readonly int MaxAllowedThreads;
        private readonly int MinAllowedThreads;
        private readonly SmartThreadPoolQueue _globalQueue;
        private readonly SmartThreadPoolQueue[] _queues = new SmartThreadPoolQueue[Math.Abs((int)ThreadPoolItemPriority.High - (int)ThreadPoolItemPriority.Low)];
        private readonly IExecutionSegment _managementSegment = new ExecutionSegment(ManagementSegmentName);
        private readonly HashSet<IExecutionSegment> _segments = new();
        private readonly ConcurrentQueue<IExecutionSegment> _parkedSegments = new();
        private readonly ManualResetEvent _event = new(false);
        private readonly SmartThreadPoolStrategy _globalStrategy;
        private volatile int _threadsCounter = 0;
        
        public SmartThreadPool(int minAllowedThreads = 1, int maxAllowedThreads = -1)
        {
            MinAllowedThreads = minAllowedThreads;
            MaxAllowedThreads = maxAllowedThreads > 0 ? maxAllowedThreads : Environment.ProcessorCount * 2;
            SynchronizationContext = new SmartThreadPoolSynchronizationContext(this);
            MaxThreadsGot = 0;
            
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
        }

        public SynchronizationContext SynchronizationContext { get; }

        public int ParallelismLevel => _segments.Count;
        public int MaxThreadsGot { get; private set; }

        public WaitHandle InitializedWaitHandle => _event;
     
        /// <summary>
        /// Initialize with regular delegate 
        /// </summary>
        public void Enqueue(ExecutionUnit unit, object state = default, bool preferLocal = true)
        {
            var unitOfWork = default(UnitOfWork);
            unitOfWork.Init(unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with regular function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<object, void> unit, object state = default, bool preferLocal = true)
        {
            UnitOfWork unitOfWork = default;
            unitOfWork.Init(unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized delegate 
        /// </summary>
        public void Enqueue(ExecutionUnit<TPoolParameter> unit, object state = default, bool preferLocal = true)
        {
            UnitOfWork unitOfWork = default;
            unitOfWork.Init(unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }
        
        /// <summary>
        /// Initialize with parametrized function pointer 
        /// </summary>
        public unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object state = default, bool preferLocal = true)
        {
            UnitOfWork unitOfWork = default;
            unitOfWork.Init((delegate*<object, object, void>)unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }
        
        public void Enqueue(ExecutionUnitAsync unit, object state = default, bool preferLocal = true)
        {
            var unitOfWork = default(UnitOfWork);
            unitOfWork.Init(unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }

        
        public void Enqueue(ExecutionUnitAsync<TPoolParameter> unit, object state = default, bool preferLocal = true)
        {
            var unitOfWork = default(UnitOfWork);
            unitOfWork.Init(unit, state);
            _globalQueue.Enqueue(unitOfWork, preferLocal);
        }
        
        public void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object state = default, TimeSpan timeout = default)
        {
            var wfsoState = new WaitForSingleObjectState
            {
                Delegate = unit,
                Timeout = timeout,
                Handle = handle,
                InternalState = state
            };

            void ExecutionUnitCallback(object args)
            {
                var workload = (WaitForSingleObjectState)args;
                workload.Handle.WaitOne(workload.Timeout);
                workload.Delegate(workload.InternalState);
            }

            var unitOfWork = default(UnitOfWork);
            unitOfWork.Init(ExecutionUnitCallback, wfsoState);
            _globalQueue.Enqueue(unitOfWork, false);
        }
        
        bool IThreadPoolThreadsManagement.CreateAdditionalExecutionSegment()
        {
            _managementSegment.SetExecutingUnit(_ =>
            {
                if (CreateAdditionalThreadImpl())
                {
                    ; // log
                }
            });
            return true;
        }

        bool IThreadPoolThreadsManagement.NotifyAboutExecutionSegmentStopping(IExecutionSegment segment)
        {
            lock (_segments)
            {
                if (_segments.Count > MinAllowedThreads)
                {
                    _segments.Remove(segment);
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
                        threadSegment = new ExecutionSegment($"{ManagementSegmentName}: #{index}");
                        _segments.Add(threadSegment);
                    }
                }
            }

            if (threadSegment != default)
            {
                var segmentLogic = new SmartThreadPoolLogic();
                var strategy = new SmartThreadPoolThreadStrategy(threadSegment, _globalStrategy);
                segmentLogic.InitializeAndStart(this, _globalQueue, strategy, threadSegment);
                MaxThreadsGot = Math.Max(MaxThreadsGot, ParallelismLevel);
                return true;
            }

            return false;
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