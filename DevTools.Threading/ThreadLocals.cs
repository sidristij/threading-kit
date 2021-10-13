using System;
using System.Collections.Concurrent;

namespace DevTools.Threading
{
    internal sealed class ThreadLocals
    {
        [ThreadStatic]
        public static ThreadLocals instance;
        public readonly IThreadPoolQueue GlobalQueue;
        public ConcurrentQueue<UnitOfWork> LocalQueue;

        private readonly ThreadsLocalQueuesList _queueList;

        public ThreadLocals(
            IThreadPoolQueue tpq,
            ThreadsLocalQueuesList queueList)
        {
            GlobalQueue = tpq;
            _queueList = queueList;
            LocalQueue = new();
            _queueList.Add(LocalQueue);
        }

        public void TransferLocalWork()
        {
            while (LocalQueue.TryDequeue(out var cb))
            {
                GlobalQueue.Enqueue(cb);
            }
        }

        ~ThreadLocals()
        {
            // Transfer any pending workitems into the global queue so that they will be executed by another thread
            if (null != LocalQueue)
            {
                TransferLocalWork();
                _queueList.Remove(LocalQueue);
            }
        }
    }
}