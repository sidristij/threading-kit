using System;
using System.Threading;

namespace DevTools.Threading
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        int ParallelismLevel { get; }
        
        public int MaxHistoricalParallelismLevel { get; }
        
        void Enqueue(PoolAction unit, object outer = default, bool preferLocal = true);
        
        unsafe void Enqueue(delegate*<object, void> unit, object outer = default, bool preferLocal = true);

        void RegisterWaitForSingleObject(WaitHandle handle, PoolAction unit, object outer = default, TimeSpan timeout = default);
    }

    public interface IThreadPool<TPoolParameter> : IThreadPool
    {
        void Enqueue(PoolAction<TPoolParameter> unit, object outer = null, bool preferLocal = true);
        
        unsafe void Enqueue(delegate*<TPoolParameter, object, void> unit, object outer = default, bool preferLocal = true);
    }
}