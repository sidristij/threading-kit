﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace DevTools.Threading
{
    internal class ThreadsLocalQueuesList
    {
        internal volatile CQueue[] _queues = new CQueue[0];

        public void Add(CQueue queue)
        {
            while (true)
            {
                var oldQueues = _queues;

                var newQueues = new CQueue[oldQueues.Length + 1];
                Array.Copy(oldQueues, newQueues, oldQueues.Length);
                newQueues[^1] = queue;
                if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                {
                    break;
                }
            }
        }

        public void Remove(CQueue queue)
        {
            while (true)
            {
                var oldQueues = _queues;
                if (oldQueues.Length == 0)
                {
                    return;
                }

                int pos = Array.IndexOf(oldQueues, queue);
                if (pos == -1)
                {
                    Debug.Fail("Should have found the queue");
                    return;
                }

                var newQueues = new CQueue[oldQueues.Length - 1];
                if (pos == 0)
                {
                    Array.Copy(oldQueues, 1, newQueues, 0, newQueues.Length);
                }
                else if (pos == oldQueues.Length - 1)
                {
                    Array.Copy(oldQueues, newQueues, newQueues.Length);
                }
                else
                {
                    Array.Copy(oldQueues, newQueues, pos);
                    Array.Copy(oldQueues, pos + 1, newQueues, pos, newQueues.Length - pos);
                }

                if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                {
                    break;
                }
            }
        }
    }
}