using System.Threading;

namespace DevTools.Threading
{
    internal class CyclicQueue<T>
    {
        private readonly Wrapper[] _array;
        private volatile int _pos = 0;
        private int _first = 1;

        public CyclicQueue(int capacity)
        {
            _array = new Wrapper[capacity];
        }
        
        public T this[int i] => _array[i].Value;
        public int Count => _array.Length;

        public void Add(T value)
        {
            // if first set, fill with value to get correct AVG
            if (Interlocked.CompareExchange(ref _first, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _first, 0);
                Interlocked.Increment(ref _pos);
                
                // looks dangerous in parallel, but setting first value isn't so important
                for (int i = 0, len = _array.Length; i < len; i++)
                {
                    _array[i].Value = value;
                }
            }
            else
            {
                var index = Interlocked.Increment(ref _pos);
                _array[index % _array.Length].Value = value;
            }
        }

        // for array access speedup
        private struct Wrapper
        {
            public T Value;
        }
    }

    internal static class CyclicQueueStatsReader
    {
        /// <summary>
        /// Gets avg of values in queue 
        /// </summary>
        public static double GetAvg(this CyclicQueue<double> queue)
        {
            var sum = 0.0;
            for (int i = 0, len = queue.Count; i < len; i++)
            {
                sum += queue[i];
            }
            return sum / queue.Count;
        }
    }
}