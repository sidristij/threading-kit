using System;
using System.Threading;

namespace DevTools.Threading
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        int ParallelismLevel { get; }
        
        void Enqueue(ExecutionUnit unit, object state = default);
        
        void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default);

        void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object state = default, TimeSpan timeout = default);
    }

    public interface IThreadPool<TPoolParameter> : IThreadPool
    {
        void Enqueue(ExecutionUnit<TPoolParameter> unit, object state = null);
    }
}