using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    public class SimpleQueue : IThreadPoolQueue, IThreadPoolInternalData
    {
        private const int MaxSemaphoreVolume = 0x7ff;
        private readonly ConcurrentQueue<UnitOfWork> _workQueue = new();
        private readonly WorkStealingQueueList _stealingQueue = new();
        
        // create with 4 threads allowed to run
        private readonly SemaphoreSlim _semaphore = new(MaxSemaphoreVolume - 4, MaxSemaphoreVolume);
        private volatile int enqueueCount = 0;
        
        WorkStealingQueueList IThreadPoolInternalData.QueueList => _stealingQueue;

        public int GlobalCount => _workQueue.Count;

        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal)
        {
            ThreadPoolWorkQueueThreadLocals tl = null;
            if (!forceGlobal)
                tl = ThreadPoolWorkQueueThreadLocals.instance;
            
            if (tl != null)
            {
                tl.workStealingQueue.LocalPush(unitOfWork);                
            }
            else
            {
                _workQueue.Enqueue(unitOfWork);
            }
        }

        public UnitOfWork Dequeue(ref bool missedSteal)
        {
            UnitOfWork unitOfWork;
            var tl = ThreadPoolWorkQueueThreadLocals.instance;
            var localWsq = tl.workStealingQueue;
            
            if ((unitOfWork = tl.workStealingQueue.LocalPop()) == null && !_workQueue.TryDequeue(out unitOfWork))
            {
                var queues = _stealingQueue.Queues;
                var c = queues.Length; 
                var maxIndex = c - 1;
                var i = Environment.TickCount % c;
                
                while (c > 0)
                {
                    i = (i < maxIndex) ? i + 1 : 0;
                    var otherQueue = queues[i];
                    if (otherQueue != localWsq && otherQueue.CanSteal)
                    {
                        unitOfWork = otherQueue.TrySteal(ref missedSteal);
                        if (unitOfWork != null)
                        {
                            return unitOfWork;
                        }
                    }
                    c--;
                }
            }

            return unitOfWork;
        }
    }
}