using System;
using System.Threading;

namespace DevTools.Threading.Abstractions
{
    public interface IThreadPool
    {
        SynchronizationContext SynchronizationContext { get; }
        
        void Enqueue(SendOrPostCallback action, object state = default);

        void RegisterWaitForSingleObject(WaitHandle handle, SendOrPostCallback action, object state = default, TimeSpan timeout = default);
    }
}