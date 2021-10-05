using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal class CrossPoolsThreadsController
    {
        private ConcurrentDictionary<ExecutionSegment, bool> _runningThreads;
        private ConcurrentQueue<ExecutionSegment> _freeThreads;

        public CrossPoolsThreadsController()
        {
            _runningThreads = new ConcurrentDictionary<ExecutionSegment, bool>();
            _freeThreads = new ConcurrentQueue<ExecutionSegment>();
        }

        public bool HasAvailableThreads => _freeThreads.IsEmpty == false;

        public bool TryRentThread(SendOrPostCallback work, out ExecutionSegment executionSegment)
        {
            if (_freeThreads.TryDequeue(out executionSegment))
            {
                _runningThreads[executionSegment] = true;
                var key = executionSegment;
                executionSegment.SetExecutingUnit(status =>
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