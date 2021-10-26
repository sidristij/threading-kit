using System.Runtime.CompilerServices;
using System.Threading;

namespace DevTools.Threading
{
    internal class CyclicTimeRangesQueue
    {
        private readonly Wrapper[] _array;
        private volatile int _pos = 0;
        private long _sum = 0;
        private long _avg;
        private readonly int _length;
        private readonly int _lastIndex;

        public CyclicTimeRangesQueue()
        {
            _array = new Wrapper[32];
            _length = 32;
            _lastIndex = _length - 1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetAvg() => _avg;

        public void Add(long value)
        {
            // if first set, fill with value to get correct AVG
            var curpos = _pos;

            if (curpos == _lastIndex)
            {
                if (Interlocked.CompareExchange(ref _pos, 0, _lastIndex) == _lastIndex)
                {
                    Interlocked.Add(ref _sum, value - _array[0].Value);
                    _array[0].Value = value;
                    return;
                }
            }  
            var index = Interlocked.Increment(ref _pos) & _lastIndex;
            Interlocked.Add(ref _sum, value - _array[index].Value);
            _array[index].Value = value;
            _avg = _sum / 32;
        }

        // for array access speedup
        private struct Wrapper
        {
            public long Value;
        }
    }
}