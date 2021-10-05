using System;
using System.Collections.Concurrent;

namespace DevTools.Threading.Abstractions
{
    public class SimpleQueue : IThreadPoolQueue, IWorkStealingQueueListProvider
    {
        private readonly ConcurrentQueue<UnitOfWork> _workQueue = new();
        private readonly WorkStealingQueueList _stealingQueue = new();

        WorkStealingQueueList IWorkStealingQueueListProvider.QueueList => _stealingQueue;
        
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

        public bool TryDequeue(out UnitOfWork unitOfWork)
        {
            var tl = ThreadPoolWorkQueueThreadLocals.instance;
            var localWsq = tl.workStealingQueue;
            var missedSteal = false;
            
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
                            return true;
                        }
                    }
                    c--;
                }

                return false;
            }

            return true;
        }
    }
}