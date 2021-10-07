using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    public class SimpleQueue : IThreadPoolQueue, IThreadPoolInternalData
    {
        // Global queue of tasks with high cost of getting items
        private readonly ConcurrentQueue<UnitOfWork> _workQueue = new();
        private volatile int _parallelCounter = 0;
        
        // Set of local for each thread queues
        private readonly WorkStealingQueueList _stealingQueue = new();

        WorkStealingQueueList IThreadPoolInternalData.QueueList => _stealingQueue;

        public int GlobalCount => _parallelCounter;

        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal)
        {
            ThreadPoolWorkQueueThreadLocals tl = null;
            if (!forceGlobal)
            {
                tl = ThreadPoolWorkQueueThreadLocals.instance;
            }

            if (tl != null)
            {
                tl.workStealingQueue.LocalPush(unitOfWork);                
            }
            else
            {
                _workQueue.Enqueue(unitOfWork);
                Interlocked.Increment(ref _parallelCounter);
            }
        }

        public UnitOfWork Dequeue(ref bool missedSteal)
        {
            UnitOfWork unitOfWork;
            var tl = ThreadPoolWorkQueueThreadLocals.instance;
            var localWsq = tl.workStealingQueue;

            if ((unitOfWork = tl.workStealingQueue.LocalPop()).InternalObjectIndex == 0)
            {
                if (!_workQueue.TryDequeue(out unitOfWork))
                {
                    var queues = _stealingQueue.Queues;
                    var c = queues.Length;
                    var maxIndex = c - 1;
                    var i = Environment.TickCount % c;
                    var stopAt = Math.Max(0, c - 2);

                    // nothing to do: try to steal work from other queues
                    while (c > stopAt)
                    {
                        i = (i < maxIndex) ? i + 1 : 0;
                        var otherQueue = queues[i];
                        if (otherQueue != localWsq && otherQueue.CanSteal)
                        {
                            unitOfWork = otherQueue.TrySteal(ref missedSteal);
                            if (unitOfWork.InternalObjectIndex != 0)
                            {
                                return unitOfWork;
                            }
                        }

                        c--;
                    }
                }
                else
                {
                    Interlocked.Decrement(ref _parallelCounter);
                }
            }

            return unitOfWork;
        }
    }
}