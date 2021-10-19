using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal class SmartThreadPoolQueue : IThreadPoolQueue, IThreadPoolInternalData
    {
        // Global queue of tasks with high cost of getting items
        private readonly ConcurrentQueue<PoolWork> _workQueue = new();
        private volatile int _globalCounter;
        
        // Set of local for each thread queues
        private readonly ThreadsLocalQueuesList _stealingQueue = new();
        
        ThreadsLocalQueuesList IThreadPoolInternalData.QueueList => _stealingQueue;

        public int GlobalCount => _globalCounter;
        
        public void Enqueue(PoolWork poolWork, bool preferLocal)
        {
            if (preferLocal)
            {
                var tl = ThreadLocals.instance;
                if (tl != null)
                {
                    tl.Enqueue(poolWork);
                    return;
                }
            }

            _workQueue.Enqueue(poolWork);
            Interlocked.Increment(ref _globalCounter);
        }

        public bool TryDequeue(ref PoolWork poolWork)
        {
            var localWsq = ThreadLocals.instance;

            // try read local queue
            if (localWsq.Count > 0 && localWsq.TryDequeue(out poolWork))
            {
                return true;
            }
            
            // try read single item
            if (_workQueue.TryDequeue(out poolWork))
            {
                Interlocked.Decrement(ref _globalCounter);
                return true;
            }

            return false;
        }
    }
}