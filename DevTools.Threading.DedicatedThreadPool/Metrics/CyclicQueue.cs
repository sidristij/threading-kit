namespace DedicatedThreadPool
{
    internal class CyclicQueue<T>
    {
        private readonly Wrapper[] _array;
        private int _pos = 0;
        private bool _first = true;

        public CyclicQueue(int capacity)
        {
            _array = new Wrapper[capacity];
        }
        
        public T this[int i] => _array[i].Value;
        public int Count => _array.Length;

        public void Add(T value)
        {
            // if first set, fill with value to get correct AVG
            if (_first)
            {
                _first = false;
                for (int i = 0, len = _array.Length; i < len; i++)
                {
                    _array[i].Value = value;
                }
            }
            else
            {
                _array[_pos].Value = value;
            }
            _pos++;
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
        public static long GetAvg(this CyclicQueue<long> queue)
        {
            var sum = 0L;
            for (int i = 0, len = queue.Count; i < len; i++)
            {
                sum += queue[i];
            }
            return sum / queue.Count;
        }
    }
}