using System;
using System.Collections.Generic;
using System.Threading;

namespace DevTools.Threading
{
    public class SimpleThreadPool<TQueueType, TThreadWrapperType> : IThreadPool, IThreadPoolThreadsManagement
        where TQueueType : IThreadPoolQueue, new() 
        where TThreadWrapperType : ExecutionSegmentLogicBase, new()
    {
        private const string ManagementSegmentName = "Management segment";
        private readonly int MaxAllowedThreads = Environment.ProcessorCount * 2;
        private readonly int MinAllowedThreads = 2;
        private readonly TQueueType _globalQueue = new();
        private readonly IExecutionSegment _managementSegment = new ExecutionSegment(ManagementSegmentName);
        private readonly HashSet<IExecutionSegment> _segments = new();
        private readonly ManualResetEvent _event = new ManualResetEvent(false);
        private readonly IThreadPoolLifetimeStrategy _globalStrategy;
        private volatile int _threadsCounter = 0;
        
        public SimpleThreadPool()
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
     
        bool IThreadPoolThreadsManagement.CreateAdditionalThread()
        {
            _managementSegment.SetExecutingUnit(_ =>
            {
                if (CreateAdditionalThreadImpl())
                {
                    Console.WriteLine($"Created additional thread #{_segments.Count}");
                }
            });
            return true;
        }

        bool IThreadPoolThreadsManagement.CheckCanStopThread()
        {
            return _segments.Count > MinAllowedThreads;
        }

        bool IThreadPoolThreadsManagement.NotifyExecutionSegmentStopping(IExecutionSegment segment)
        {
            lock (_segments)
            {
                if (_segments.Count > MinAllowedThreads)
                {
                    _segments.Remove(segment);
                    return true;
                }
            }

            return false;
        }

        public void Enqueue(ExecutionUnit unit, object state = default)
        {
            var unitOfWork = new UnitOfWork(unit, ThreadPoolItemPriority.Default, state);
            _globalQueue.Enqueue(unitOfWork);
        }

        public void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default)
        {
            var unitOfWork = new UnitOfWork(unit, priority, state);
            _globalQueue.Enqueue(unitOfWork);
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

            var unitOfWork = new UnitOfWork(ExecutionUnitCallback, ThreadPoolItemPriority.Default, wfsoState);
            _globalQueue.Enqueue(unitOfWork);
        }
        
        private bool CreateAdditionalThreadImpl()
        {
            ExecutionSegment threadSegment = default;
            
            lock (_segments)
            {
                if (_segments.Count < MaxAllowedThreads)
                {
                    var index = Interlocked.Increment(ref _threadsCounter);
                    threadSegment = new ExecutionSegment($"{ManagementSegmentName}: #{index}");
                    _segments.Add(threadSegment);
                }
            }

            if(threadSegment != default)
            {
                var segmentLogic = new TThreadWrapperType();
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