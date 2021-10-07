using System;
using System.Threading;

namespace DevTools.Threading
{
    internal sealed class ThreadPoolWorkQueueThreadLocals
    {
        [ThreadStatic]
        public static ThreadPoolWorkQueueThreadLocals? instance;

        public readonly IThreadPoolQueue workQueue;
        private readonly WorkStealingQueueList _queueList;
        public readonly WorkStealingQueue workStealingQueue;
        public readonly Thread currentThread;

        public ThreadPoolWorkQueueThreadLocals(
            IThreadPoolQueue tpq,
            WorkStealingQueueList queueList)
        {
            workQueue = tpq;
            _queueList = queueList;
            workStealingQueue = new WorkStealingQueue();
            _queueList.Add(workStealingQueue);
            currentThread = Thread.CurrentThread;
        }

        public void TransferLocalWork()
        {
            while (workStealingQueue.LocalPop() is UnitOfWork cb)
            {
                workQueue.Enqueue(cb);
            }
        }

        ~ThreadPoolWorkQueueThreadLocals()
        {
            // Transfer any pending workitems into the global queue so that they will be executed by another thread
            if (null != workStealingQueue)
            {
                TransferLocalWork();
                _queueList.Remove(workStealingQueue);
            }
        }
    }
}