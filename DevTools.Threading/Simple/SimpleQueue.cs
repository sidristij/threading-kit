using System;
using System.Collections.Concurrent;
using System.Threading;
using DevTools.Threadinglf;

namespace DevTools.Threading
{
    internal class SimpleQueue : IThreadPoolQueue, IThreadPoolInternalData
    {
        // Global queue of tasks with high cost of getting items
        // private readonly ConcurrentQueue<UnitOfWork> _workQueue = new();
        private readonly ConcurrentQueueWithSegmentsAccess<UnitOfWork> _workQueue = new();
        private volatile int _parallelCounter = 0;
        
        // Set of local for each thread queues
        private readonly ThreadsLocalQueuesList _stealingQueue = new();

        ThreadsLocalQueuesList IThreadPoolInternalData.QueueList => _stealingQueue;

        public int GlobalCount => _parallelCounter;

        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal)
        {
            ThreadLocals tl = null;
            if (!forceGlobal)
            {
                tl = ThreadLocals.instance;
            }

            if (tl != null)
            {
                tl.LocalQueue.Enqueue(unitOfWork);                
            }
            else
            {
                _workQueue.Enqueue(unitOfWork);
                Interlocked.Increment(ref _parallelCounter);
            }
        }

        public void Dequeue(ref UnitOfWork unitOfWork, ref ConcurrentQueueSegment<UnitOfWork> segment)
        {
            var tl = ThreadLocals.instance;
            var localWsq = tl.LocalQueue;

            if (tl.LocalQueue.TryDequeue(out unitOfWork) == false)
            {
                if (_workQueue.TryDequeueSegment(out segment) == false)
                {
                    if (_workQueue.TryDequeue(out unitOfWork) == false)
                    {
                        var queues = _stealingQueue._queues;
                        var c = queues.Length;
                        var maxIndex = c - 1;
                        var i = Environment.TickCount % c;
                        var stopAt = Math.Max(0, c - 2);
                    
                        // nothing to do: try to steal work from other queues
                        while (c > stopAt)
                        {
                            i = (i < maxIndex) ? i + 1 : 0;
                            var otherQueue = queues[i];
                            if (otherQueue != localWsq && otherQueue.HasAny && otherQueue.TryDequeue(out unitOfWork))
                            {
                                if (unitOfWork.InternalObjectIndex != 0)
                                {
                                    return;
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
                else
                {
                    Interlocked.Add(ref _parallelCounter, 0 - segment.Capacity);
                }
            }
        }
    }
}