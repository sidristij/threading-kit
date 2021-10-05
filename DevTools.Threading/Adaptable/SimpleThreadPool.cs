using System;
using System.Collections.Generic;
using System.Threading;
using DevTools.MemoryPools;
using DevTools.Threading.Abstractions;

namespace DevTools.Threading
{
    public class SimpleThreadPool<TQueueType, TThreadWrapperType> : IThreadPool 
        where TQueueType : IThreadPoolQueue, new() 
        where TThreadWrapperType : ExecutionSegmentLogicBase, new()
    {
        private const string ManagementSegmentName = "Management segment";
        private readonly TQueueType _queue = new();
        private readonly IExecutionSegment _managementSegment = new ExecutionSegment(ManagementSegmentName);
        private readonly HashSet<IExecutionSegment> _segments = new();
        private readonly ManualResetEvent _event = new ManualResetEvent(false);
        
        public SimpleThreadPool()
        {
            SynchronizationContext = new DedicatedSynchronizationContext(this);
            _managementSegment.SetExecutingUnit(_ =>
            {
                for (var i = 0; i < Environment.ProcessorCount; i++)
                {
                    var threadSegment = new ExecutionSegment($"{ManagementSegmentName}: #{i}");
                    _segments.Add(threadSegment);

                    var segmentLogic = new TThreadWrapperType();
                    segmentLogic.InitializeAndStart(this, _queue, threadSegment);
                }
                _event.Set();
            });
        }

        public SynchronizationContext SynchronizationContext { get; }

        public WaitHandle InitializedWaitHandle => _event;
        
        public void Enqueue(ExecutionUnit unit, object state = default)
        {
            var unitOfWork = Heap.Get<UnitOfWork>().Initialize(unit, ThreadPoolItemPriority.Default, state);
            _queue.Enqueue(unitOfWork);
        }

        public void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default)
        {
            var unitOfWork = Heap.Get<UnitOfWork>().Initialize(unit, priority, state);
            _queue.Enqueue(unitOfWork);
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

            ExecutionUnit callback = args =>
            {
                var workload = (WaitForSingleObjectState)args;
                workload.Handle.WaitOne(workload.Timeout);
                workload.Delegate(workload.InternalState);
            }; 
            
            var unitOfWork = Heap.Get<UnitOfWork>().Initialize(callback, ThreadPoolItemPriority.Default, wfsoState);
            _queue.Enqueue(unitOfWork);
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