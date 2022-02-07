using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DevTools.Threading
{
    internal class SmartThreadPoolQueue
    {
        // Global queue of tasks with high cost of getting items
        private readonly ConcurrentQueue<PoolActionUnit> _workQueue = new();
        private volatile int _globalCounter;
        
        public int GlobalCount => _globalCounter;
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Enqueue(ref PoolActionUnit poolActionUnit)
        {
            _workQueue.Enqueue(poolActionUnit);
            Interlocked.Increment(ref _globalCounter);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool TryDequeue(out PoolActionUnit poolActionUnit)
        {
            // try read single item
            if (_workQueue.TryDequeue(out poolActionUnit))
            {
                Interlocked.Decrement(ref _globalCounter);
                return true;
            }
            return false;
        }
    }
}