using System.Collections.Concurrent;
using System.Threading;

namespace DedicatedThreadPool
{
    internal class CrossPoolsThreadsController
    {
        private ConcurrentDictionary<ControllableThread, bool> _runningThreads;
        private ConcurrentQueue<ControllableThread> _freeThreads;

        public CrossPoolsThreadsController()
        {
            _runningThreads = new ConcurrentDictionary<ControllableThread, bool>();
            _freeThreads = new ConcurrentQueue<ControllableThread>();
        }

        public bool HasAvailableThreads => _freeThreads.IsEmpty == false;

        public bool TryRentThread(SendOrPostCallback work, out ControllableThread thread)
        {
            if (_freeThreads.TryDequeue(out thread))
            {
                _runningThreads[thread] = true;
                var key = thread;
                thread.SetRunningDelegate(status =>
                {
                    work(status);
                    _runningThreads.TryRemove(key, out _);
                    _freeThreads.Enqueue(key);
                });
                return true;
            }

            return false;
        }
    }
}