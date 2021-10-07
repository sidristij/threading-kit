using System.Runtime.CompilerServices;
using System.Threading;

namespace DevTools.Threading
{
    internal class CyclicQueue<T>
    {
        private readonly Wrapper[] _array;
        private volatile int _pos = 0;
        private int _first = 1;
        private readonly int _length;
        private readonly int _lastIndex;

        public CyclicQueue()
        {
            _array = new Wrapper[8];
            _length = 8;
            _lastIndex = _length - 1;
        }
        
        public T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array[i].Value;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        public void Add(T value)
        {
            // if first set, fill with value to get correct AVG
            if (Interlocked.CompareExchange(ref _first, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _first, 0);
                Interlocked.Increment(ref _pos);
                
                // looks dangerous in parallel, but setting first value isn't so important
                for (int i = 0, len = _length; i < len; i++)
                {
                    _array[i].Value = value;
                }
            }
            else
            {
                var curpos = _pos;
                int index;
                if (curpos == _lastIndex)
                {
                    if (Interlocked.CompareExchange(ref _pos, 0, _lastIndex) == _lastIndex)
                    {
                        _array[0].Value = value;
                        return;
                    }
                }

                index = Interlocked.Increment(ref _pos);
                
                _array[index & _lastIndex].Value = value;
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
        public static double GetAvg(this CyclicQueue<float> queue)
        {
            return (queue[0] + queue[1] + queue[2] + queue[3] +
                    queue[4] + queue[5] + queue[6] + queue[7]) / 8;
        }
    }
}