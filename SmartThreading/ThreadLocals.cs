using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal sealed class ThreadLocals
    {
        [ThreadStatic]
        public static ThreadLocals instance;

        public readonly IThreadPoolQueue GlobalQueue;
        public volatile int Count;

        private readonly ConcurrentQueue<UnitOfWork> _localQueue;
        private readonly ThreadsLocalQueuesList _queueList;

        public ThreadLocals(
            IThreadPoolQueue tpq,
            ThreadsLocalQueuesList queueList)
        {
            GlobalQueue = tpq;
            _queueList = queueList;
            _localQueue = new();
            _queueList.Add(_localQueue);
        }

        public void Enqueue(UnitOfWork unitOfWork)
        {
            Interlocked.Increment(ref Count);
            _localQueue.Enqueue(unitOfWork);
        }
        
        public bool TryDequeue(out UnitOfWork unitOfWork)
        {
            if (_localQueue.TryDequeue(out unitOfWork))
            {
                Interlocked.Decrement(ref Count);
                return true;
            }

            return false;
        }

        public void TransferLocalWork()
        {
            while (_localQueue.TryDequeue(out var cb))
            {
                GlobalQueue.Enqueue(cb);
            }
        }

        ~ThreadLocals()
        {
            // Transfer any pending workitems into the global queue so that they will be executed by another thread
            if (null != _localQueue)
            {
                TransferLocalWork();
                _queueList.Remove(_localQueue);
            }
        }
    }
}