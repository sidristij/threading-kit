using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DevTools.Threading
{
    public class SmartThreadPool<TPoolParameter> : IThreadPool<TPoolParameter>, IThreadPoolThreadsManagement
    {
        private const string ManagementSegmentName = "Management segment";
        private readonly int MaxAllowedThreads = Environment.ProcessorCount * 2;
        private readonly int MinAllowedThreads = 4;
        private readonly SimpleQueue _globalQueue = new();
        private readonly IExecutionSegment _managementSegment = new ExecutionSegment(ManagementSegmentName);
        private readonly HashSet<IExecutionSegment> _segments = new();
        private readonly ConcurrentQueue<IExecutionSegment> _parkedSegments = new();
        private readonly ManualResetEvent _event = new ManualResetEvent(false);
        private readonly IThreadPoolLifetimeStrategy _globalStrategy;
        private volatile int _threadsCounter = 0;
        
        public SmartThreadPool()
        {
            SynchronizationContext = new DedicatedSynchronizationContext(this);
            _globalStrategy = new SimpleThreadPoolLifetimeStrategy(this);
            _managementSegment.SetExecutingUnit(_ =>
            {
                for (var i = 0; i < Environment.ProcessorCount; i++)
                {
                    CreateAdditionalThreadImpl();
                }
                _event.Set();
            });
        }

        public SynchronizationContext SynchronizationContext { get; }

        public int ParallelismLevel => _segments.Count;

        public WaitHandle InitializedWaitHandle => _event;
     
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

        bool IThreadPoolThreadsManagement.NotifyExecutionSegmentStopping(IExecutionSegment segment)
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

        public void Enqueue(ExecutionUnit unit, object state = default)
        {
            var unitOfWork = new UnitOfWork(unit, state);
            _globalQueue.Enqueue(unitOfWork, false);
        }

        public void Enqueue(ExecutionUnit<TPoolParameter> unit, object state = default)
        {
            var unitOfWork = new UnitOfWork(Unsafe.As<ExecutionUnit>(unit), state);
            _globalQueue.Enqueue(unitOfWork, false);
        }
        
        public void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default)
        {
            var unitOfWork = new UnitOfWork(unit, state);
            _globalQueue.Enqueue(unitOfWork, false);
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

            var unitOfWork = new UnitOfWork(ExecutionUnitCallback, wfsoState);
            _globalQueue.Enqueue(unitOfWork, false);
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
                var segmentLogic = new SimpleLogic();
                var strategy = new SimpleThreadPoolThreadLifetimeStrategy(threadSegment, _globalStrategy);
                segmentLogic.InitializeAndStart(this, _globalQueue, strategy, threadSegment);
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