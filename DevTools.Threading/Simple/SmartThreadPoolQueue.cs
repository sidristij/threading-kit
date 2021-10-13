using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal class SmartThreadPoolQueue : IThreadPoolQueue, IThreadPoolInternalData
    {
        // Global queue of tasks with high cost of getting items
        private readonly ConcurrentQueue<UnitOfWork> _workQueue = new();
        private volatile int _parallelCounter;
        
        // Set of local for each thread queues
        private readonly ThreadsLocalQueuesList _stealingQueue = new();
        
        ThreadsLocalQueuesList IThreadPoolInternalData.QueueList => _stealingQueue;

        public int GlobalCount => _parallelCounter;

        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal)
        {
            // ThreadLocals tl = null;
            // if (!forceGlobal)
            // {
            //     tl = ThreadLocals.instance;
            // }
            //
            // if (tl != null)
            // {
            //     tl.LocalQueue.Enqueue(unitOfWork);
            //     return;
            // }
            
            _workQueue.Enqueue(unitOfWork);
            Interlocked.Increment(ref _parallelCounter);
        }

        public void Dequeue(ref UnitOfWork unitOfWork)
        {
            // var localWsq = ThreadLocals.instance.LocalQueue;

            // try read local queue
            // if (localWsq.TryDequeue(out unitOfWork))
            // {
            //     Interlocked.Decrement(ref _parallelCounter);
            //     return;
            // }

            // try read single item
            if (_workQueue.TryDequeue(out unitOfWork))
            {
                Interlocked.Decrement(ref _parallelCounter);
            }
        }
    }
}