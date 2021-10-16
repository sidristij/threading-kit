using System;
using System.Threading;

namespace DevTools.Threading
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        int ParallelismLevel { get; }
        
        public int MaxHistoricalParallelismLevel { get; }
        
        unsafe void Enqueue(delegate*<object, void> unit, object outer = default, bool preferLocal = true);
        
        void Enqueue(ExecutionUnit unit, object outer = default, bool preferLocal = true);
        
        void RegisterWaitForSingleObject(WaitHandle handle, ExecutionUnit unit, object outer = default, TimeSpan timeout = default);
    }

    public interface IThreadPool<TPoolParameter> : IThreadPool
    {
        void Enqueue(ExecutionUnit<TPoolParameter> unit, object outer = null, bool preferLocal = true);
        unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object outer = default, bool preferLocal = true);
    }
}