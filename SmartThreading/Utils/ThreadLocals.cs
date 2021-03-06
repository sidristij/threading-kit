using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal sealed class ThreadLocals
    {
        [ThreadStatic]
        public static ThreadLocals instance;

        public volatile int Count;

        private readonly IThreadPoolQueue _globalQueue;
        private readonly ConcurrentQueue<PoolActionUnit> _localQueue;
        private readonly ThreadsLocalQueuesList _queueList;

        public ThreadLocals(
            IThreadPoolQueue tpq,
            ThreadsLocalQueuesList queueList)
        {
            _globalQueue = tpq;
            _queueList = queueList;
            _localQueue = new();
            _queueList.Add(_localQueue);
        }

        public void Enqueue(PoolActionUnit poolActionUnit)
        {
            Interlocked.Increment(ref Count);
            _localQueue.Enqueue(poolActionUnit);
        }
        
        public bool TryDequeue(out PoolActionUnit poolActionUnit)
        {
            if (_localQueue.TryDequeue(out poolActionUnit))
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
                _globalQueue.Enqueue(cb);
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