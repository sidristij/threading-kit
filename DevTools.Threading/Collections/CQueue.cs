using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevTools.Threading.Collections
{
    [StructLayout(LayoutKind.Explicit, Size = CACHE_LINE_SIZE * 3)]
    public class CQueue
    {
        internal const int CACHE_LINE_SIZE = 64;
        [FieldOffset(CACHE_LINE_SIZE * 1)]
        private volatile Node _head;
        [FieldOffset(CACHE_LINE_SIZE * 2)]
        private volatile Node _tail;

        public CQueue()
        {
            _head = _tail = new Node(default);
        }

        public bool HasAny
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _head.next == default;
        }

        public void Enqueue(UnitOfWork element)
        {
            // initialize new tail
            var newTail = new Node(element);
            
            // set _last
            Node oldTail;
            do
            {
                oldTail = _tail;
            } while (Interlocked.CompareExchange(ref _tail, newTail, oldTail) != oldTail);

            // == _frst.next for first set
            oldTail.next = newTail;
        }

        public bool TryDequeue(out UnitOfWork element)
        {
            Node current;

            do
            {
                current = _head.next;
            } while (current != null && Interlocked.CompareExchange(ref _head.next, current.next, current) != current);

            if (current == default)
            {
                element = default;
                return false;
            }

            element = current.value;
            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = CACHE_LINE_SIZE * 4)]
        private class Node
        {
            [FieldOffset(CACHE_LINE_SIZE * 1)]
            public Node next;
            [FieldOffset(CACHE_LINE_SIZE * 2)]
            public UnitOfWork value;
            
            public Node(UnitOfWork val)
            {
                value = val;
            }
            
            public override string ToString()
            {
                const string nil = "nil";
                var nxt = next == null ? nil : next.value.ToString();
                return $"[{value}]->{nxt}";
            }
        }
    }
}