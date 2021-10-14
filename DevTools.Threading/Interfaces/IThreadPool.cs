using System;
using System.Threading;

namespace DevTools.Threading
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        int ParallelismLevel { get; }
        
        unsafe void Enqueue(delegate*<object, void> unit, object state = default, bool preferLocal = true);
        
        void Enqueue(ExecutionUnit unit, object state = default, bool preferLocal = true);
        
        // void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default, bool preferLocal = true);

        void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object state = default, TimeSpan timeout = default);
    }

    public interface IThreadPool<TPoolParameter> : IThreadPool
    {
        void Enqueue(ExecutionUnit<TPoolParameter> unit, object state = null, bool preferLocal = true);
        unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object state = default, bool preferLocal = true);
    }
}