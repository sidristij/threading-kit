using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DevTools.Threading
{
    public class PrioritizedQueue : IThreadPoolQueue
    {
        private List<ConcurrentQueue<UnitOfWork>> _queues = new();
        private volatile int _volume;

        public PrioritizedQueue()
        {
            for (int i = (int)ThreadPoolItemPriority.RangeStart; i <= (int)ThreadPoolItemPriority.RangeEnd; i++)
            {
                _queues.Add(new ConcurrentQueue<UnitOfWork>());
            }
        }

        public int GlobalCount => _volume;

        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal)
        {
            var priority = ThreadPoolItemPriority.Default;
            _queues[(int)priority].Enqueue(unitOfWork);
            Interlocked.Increment(ref _volume);
        }

        public UnitOfWork Dequeue(ref bool missedSteal)
        {
            for (int i = (int)ThreadPoolItemPriority.RangeStart; i < (int)ThreadPoolItemPriority.RangeEnd; i++)
            {
                if (_queues[i].TryDequeue(out var unitOfWork))
                {
                    Interlocked.Decrement(ref _volume);
                    return unitOfWork;
                }
            }

            return default;
        }
    }
}