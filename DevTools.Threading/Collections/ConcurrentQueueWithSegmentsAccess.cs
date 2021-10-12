using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DevTools.Threading
{
    /// <summary>
    /// Represents a thread-safe first-in, first-out collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    /// <remarks>
    /// All public and protected members of <see cref="ConcurrentQueue{T}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
    public class ConcurrentQueueWithSegmentsAccess<T>
    {
        private const int InitialSegmentLength = 32;
        private const int MaxSegmentLength = 1024 * 1024;
        private readonly object _crossSegmentLock;
        private volatile ConcurrentQueueSegment<T> _tail;
        private volatile ConcurrentQueueSegment<T> _head; // SOS's ThreadPool command depends on this name

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentQueue{T}"/> class.
        /// </summary>
        public ConcurrentQueueWithSegmentsAccess()
        {
            _crossSegmentLock = new object();
            _tail = _head = new ConcurrentQueueSegment<T>(InitialSegmentLength);
        }

        public void Enqueue(T item)
        {
            // Try to enqueue to the current tail.
            if (!_tail.TryEnqueue(item))
            {
                // If we're unable to, we need to take a slow path that will
                // try to add a new tail segment.
                EnqueueSlow(item);
            }
        }

        /// <summary>Adds to the end of the queue, adding a new segment if necessary.</summary>
        private void EnqueueSlow(T item)
        {
            while (true)
            {
                ConcurrentQueueSegment<T> tail = _tail;

                // Try to append to the existing tail.
                if (tail.TryEnqueue(item))
                {
                    return;
                }

                // If we were unsuccessful, take the lock so that we can compare and manipulate
                // the tail.  Assuming another enqueuer hasn't already added a new segment,
                // do so, then loop around to try enqueueing again.
                lock (_crossSegmentLock)
                {
                    if (tail == _tail)
                    {
                        // Make sure no one else can enqueue to this segment.
                        tail.EnsureFrozenForEnqueues();

                        int nextSize = tail._preservedForObservation ? InitialSegmentLength : Math.Min(tail.Capacity * 2, MaxSegmentLength);
                        var newTail = new ConcurrentQueueSegment<T>(nextSize);

                        // Hook up the new tail.
                        tail._nextSegment = newTail;
                        _tail = newTail;
                    }
                }
            }
        }

        public bool TryDequeue([MaybeNullWhen(false)] out T result)
        {
            // Get the current head
            ConcurrentQueueSegment<T> head = _head;

            // Try to take.  If we're successful, we're done.
            if (head.TryDequeue(out result))
            {
                return true;
            }

            // Check to see whether this segment is the last. If it is, we can consider
            // this to be a moment-in-time empty condition (even though between the TryDequeue
            // check and this check, another item could have arrived).
            if (head._nextSegment == null)
            {
                result = default!;
                return false;
            }

            return TryDequeueSlow(out result); // slow path that needs to fix up segments
        }

        /// <summary>Tries to dequeue an item, removing empty segments as needed.</summary>
        private bool TryDequeueSlow([MaybeNullWhen(false)] out T item)
        {
            while (true)
            {
                // Get the current head
                ConcurrentQueueSegment<T> head = _head;

                // Try to take.  If we're successful, we're done.
                if (head.TryDequeue(out item))
                {
                    return true;
                }

                // Check to see whether this segment is the last. If it is, we can consider
                // this to be a moment-in-time empty condition (even though between the TryDequeue
                // check and this check, another item could have arrived).
                if (head._nextSegment == null)
                {
                    item = default;
                    return false;
                }

                // At this point we know that head.Next != null, which means
                // this segment has been frozen for additional enqueues. But between
                // the time that we ran TryDequeue and checked for a next segment,
                // another item could have been added.  Try to dequeue one more time
                // to confirm that the segment is indeed empty.
                Debug.Assert(head._frozenForEnqueues);
                if (head.TryDequeue(out item))
                {
                    return true;
                }

                // This segment is frozen (nothing more can be added) and empty (nothing is in it).
                // Update head to point to the next segment in the list, assuming no one's beat us to it.
                lock (_crossSegmentLock)
                {
                    if (head == _head)
                    {
                        _head = head._nextSegment;
                    }
                }
            }
        }

    }

    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    [StructLayout(LayoutKind.Explicit, Size = 3 * PaddingHelpers.CACHE_LINE_SIZE)] // padding before/between/after fields
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(1 * PaddingHelpers.CACHE_LINE_SIZE)] public int Head;
        [FieldOffset(2 * PaddingHelpers.CACHE_LINE_SIZE)] public int Tail;
    }

    internal static class PaddingHelpers
    {
        public const int CACHE_LINE_SIZE = 64;
    }
}