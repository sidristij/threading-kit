using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal class SmartThreadPoolQueue : IThreadPoolQueue, IThreadPoolInternals
    {
        // Global queue of tasks with high cost of getting items
        private readonly ConcurrentQueue<PoolActionUnit> _workQueue = new();
        private volatile int _globalCounter;
        
        // Set of local for each thread queues
        private readonly ThreadsLocalQueuesList _stealingQueue = new();
        
        ThreadsLocalQueuesList IThreadPoolInternals.QueueList => _stealingQueue;

        public int GlobalCount => _globalCounter;
        
        public void Enqueue(PoolActionUnit poolActionUnit, bool preferLocal)
        {
            if (preferLocal)
            {
                var tl = ThreadLocals.instance;
                if (tl != null)
                {
                    tl.Enqueue(poolActionUnit);
                    return;
                }
            }

            _workQueue.Enqueue(poolActionUnit);
            Interlocked.Increment(ref _globalCounter);
        }

        public bool TryDequeue(out PoolActionUnit poolActionUnit)
        {
            var localWsq = ThreadLocals.instance;

            // try read local queue
            if (localWsq.Count > 0 && localWsq.TryDequeue(out poolActionUnit))
            {
                return true;
            }
            
            // try read single item
            if (_workQueue.TryDequeue(out poolActionUnit))
            {
                Interlocked.Decrement(ref _globalCounter);
                return true;
            }

            return false;
        }
    }
}