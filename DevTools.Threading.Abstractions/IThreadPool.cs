using System;
using System.Threading;

namespace DevTools.Threading.Abstractions
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        void Enqueue(ExecutionUnit unit, object state = default);
        
        void Enqueue(ExecutionUnit unit, ThreadPoolItemPriority priority, object state = default);

        void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object state = default, TimeSpan timeout = default);
    }
}