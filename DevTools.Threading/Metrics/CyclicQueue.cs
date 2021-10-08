using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevTools.Threading
{
    internal class CyclicQueue
    {
        private readonly Wrapper[] _array;
        private volatile int _pos = 0;
        private long _sum = 0;
        private float _avg;
        private readonly int _length;
        private readonly int _lastIndex;

        public CyclicQueue()
        {
            _array = new Wrapper[32];
            _length = 32;
            _lastIndex = _length - 1;
        }
        
        public float this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array[i].Value;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetAvg() => _avg;

        public void Add(float value)
        {
            // if first set, fill with value to get correct AVG
            var curpos = _pos;

            if (curpos == _lastIndex)
            {
                if (Interlocked.CompareExchange(ref _pos, 0, _lastIndex) == _lastIndex)
                {
                    Interlocked.Add(ref _sum, (long)((value - _array[0].Value) * 100000));
                    _array[0].Value = value;
                    return;
                }
            }

            var index = Interlocked.Increment(ref _pos) & _lastIndex;
            Interlocked.Add(ref _sum, (long)((value - _array[index].Value) * 100000));
            _array[index].Value = value;
            _avg = _sum / 10000;
        }

        // for array access speedup
        private struct Wrapper
        {
            public float Value;
        }
    }
}