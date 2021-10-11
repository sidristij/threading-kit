using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevTools.Threadinglf
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
        private const int InitialSegmentLength = 64;
        private const int MaxSegmentLength = 1024*1024;
 
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
        
        public bool IsEmpty =>
            !TryPeek(out _, resultUsed: false);
 
        public int Count
        {
            get
            {
                SpinWait spinner = default;
                while (true)
                {
                    // Capture the head and tail, as well as the head's head and tail.
                    ConcurrentQueueSegment<T> head = _head;
                    ConcurrentQueueSegment<T> tail = _tail;
                    int headHead = Volatile.Read(ref head._headAndTail.Head);
                    int headTail = Volatile.Read(ref head._headAndTail.Tail);
 
                    if (head == tail)
                    {
                        // There was a single segment in the queue.  If the captured segments still
                        // match, then we can trust the values to compute the segment's count. (It's
                        // theoretically possible the values could have looped around and still exactly match,
                        // but that would required at least ~4 billion elements to have been enqueued and
                        // dequeued between the reads.)
                        if (head == _head &&
                            tail == _tail &&
                            headHead == Volatile.Read(ref head._headAndTail.Head) &&
                            headTail == Volatile.Read(ref head._headAndTail.Tail))
                        {
                            return GetCount(head, headHead, headTail);
                        }
                    }
                    else if (head._nextSegment == tail)
                    {
                        // There were two segments in the queue.  Get the positions from the tail, and as above,
                        // if the captured values match the previous reads, return the sum of the counts from both segments.
                        int tailHead = Volatile.Read(ref tail._headAndTail.Head);
                        int tailTail = Volatile.Read(ref tail._headAndTail.Tail);
                        if (head == _head &&
                            tail == _tail &&
                            headHead == Volatile.Read(ref head._headAndTail.Head) &&
                            headTail == Volatile.Read(ref head._headAndTail.Tail) &&
                            tailHead == Volatile.Read(ref tail._headAndTail.Head) &&
                            tailTail == Volatile.Read(ref tail._headAndTail.Tail))
                        {
                            return GetCount(head, headHead, headTail) + GetCount(tail, tailHead, tailTail);
                        }
                    }
                    else
                    {
                        // There were more than two segments in the queue.  Fall back to taking the cross-segment lock,
                        // which will ensure that the head and tail segments we read are stable (since the lock is needed to change them);
                        // for the two-segment case above, we can simply rely on subsequent comparisons, but for the two+ case, we need
                        // to be able to trust the internal segments between the head and tail.
                        lock (_crossSegmentLock)
                        {
                            // Now that we hold the lock, re-read the previously captured head and tail segments and head positions.
                            // If either has changed, start over.
                            if (head == _head && tail == _tail)
                            {
                                // Get the positions from the tail, and as above, if the captured values match the previous reads,
                                // we can use the values to compute the count of the head and tail segments.
                                int tailHead = Volatile.Read(ref tail._headAndTail.Head);
                                int tailTail = Volatile.Read(ref tail._headAndTail.Tail);
                                if (headHead == Volatile.Read(ref head._headAndTail.Head) &&
                                    headTail == Volatile.Read(ref head._headAndTail.Tail) &&
                                    tailHead == Volatile.Read(ref tail._headAndTail.Head) &&
                                    tailTail == Volatile.Read(ref tail._headAndTail.Tail))
                                {
                                    // We got stable values for the head and tail segments, so we can just compute the sizes
                                    // based on those and add them. Note that this and the below additions to count may overflow: previous
                                    // implementations allowed that, so we don't check, either, and it is theoretically possible for the
                                    // queue to store more than int.MaxValue items.
                                    int count = GetCount(head, headHead, headTail) + GetCount(tail, tailHead, tailTail);
 
                                    // Now add the counts for each internal segment. Since there were segments before these,
                                    // for counting purposes we consider them to start at the 0th element, and since there is at
                                    // least one segment after each, each was frozen, so we can count until each's frozen tail.
                                    // With the cross-segment lock held, we're guaranteed that all of these internal segments are
                                    // consistent, as the head and tail segment can't be changed while we're holding the lock, and
                                    // dequeueing and enqueueing can only be done from the head and tail segments, which these aren't.
                                    for (ConcurrentQueueSegment<T> s = head._nextSegment!; s != tail; s = s._nextSegment!)
                                    {
                                        Debug.Assert(s._frozenForEnqueues, "Internal segment must be frozen as there's a following segment.");
                                        count += s._headAndTail.Tail - s.FreezeOffset;
                                    }
 
                                    return count;
                                }
                            }
                        }
                    }
 
                    // We raced with enqueues/dequeues and captured an inconsistent picture of the queue.
                    // Spin and try again.
                    spinner.SpinOnce();
                }
            }
        }
 
        /// <summary>Computes the number of items in a segment based on a fixed head and tail in that segment.</summary>
        private static int GetCount(ConcurrentQueueSegment<T> s, int head, int tail)
        {
            if (head != tail && head != tail - s.FreezeOffset)
            {
                head &= s._slotsMask;
                tail &= s._slotsMask;
                return head < tail ? tail - head : s._slots.Length - head + tail;
            }
            return 0;
        }
 
        /// <summary>Gets the number of items in snapped region.</summary>
        private static long GetCount(ConcurrentQueueSegment<T> head, int headHead, ConcurrentQueueSegment<T> tail, int tailTail)
        {
            // All of the segments should have been both frozen for enqueues and preserved for observation.
            // Validate that here for head and tail; we'll validate it for intermediate segments later.
            Debug.Assert(head._preservedForObservation);
            Debug.Assert(head._frozenForEnqueues);
            Debug.Assert(tail._preservedForObservation);
            Debug.Assert(tail._frozenForEnqueues);
 
            long count = 0;
 
            // Head segment.  We've already marked it as frozen for enqueues, so its tail position is fixed,
            // and we've already marked it as preserved for observation (before we grabbed the head), so we
            // can safely enumerate from its head to its tail and access its elements.
            int headTail = (head == tail ? tailTail : Volatile.Read(ref head._headAndTail.Tail)) - head.FreezeOffset;
            if (headHead < headTail)
            {
                // Mask the head and tail for the head segment
                headHead &= head._slotsMask;
                headTail &= head._slotsMask;
 
                // Increase the count by either the one or two regions, based on whether tail
                // has wrapped to be less than head.
                count += headHead < headTail ?
                    headTail - headHead :
                    head._slots.Length - headHead + headTail;
            }
 
            // We've enumerated the head.  If the tail is different from the head, we need to
            // enumerate the remaining segments.
            if (head != tail)
            {
                // Count the contents of each segment between head and tail, not including head and tail.
                // Since there were segments before these, for our purposes we consider them to start at
                // the 0th element, and since there is at least one segment after each, each was frozen
                // by the time we snapped it, so we can iterate until each's frozen tail.
                for (ConcurrentQueueSegment<T> s = head._nextSegment!; s != tail; s = s._nextSegment!)
                {
                    Debug.Assert(s._preservedForObservation);
                    Debug.Assert(s._frozenForEnqueues);
                    count += s._headAndTail.Tail - s.FreezeOffset;
                }
 
                // Finally, enumerate the tail.  As with the intermediate segments, there were segments
                // before this in the snapped region, so we can start counting from the beginning. Unlike
                // the intermediate segments, we can't just go until the Tail, as that could still be changing;
                // instead we need to go until the tail we snapped for observation.
                count += tailTail - tail.FreezeOffset;
            }
 
            // Return the computed count.
            return count;
        }
 
        /// <summary>Gets the item stored in the <paramref name="i"/>th entry in <paramref name="segment"/>.</summary>
        private static T GetItemWhenAvailable(ConcurrentQueueSegment<T> segment, int i)
        {
            Debug.Assert(segment._preservedForObservation);
 
            // Get the expected value for the sequence number
            int expectedSequenceNumberAndMask = (i + 1) & segment._slotsMask;
 
            // If the expected sequence number is not yet written, we're still waiting for
            // an enqueuer to finish storing it.  Spin until it's there.
            if ((segment._slots[i].SequenceNumber & segment._slotsMask) != expectedSequenceNumberAndMask)
            {
                SpinWait spinner = default;
                while ((Volatile.Read(ref segment._slots[i].SequenceNumber) & segment._slotsMask) != expectedSequenceNumberAndMask)
                {
                    spinner.SpinOnce();
                }
            }
 
            // Return the value from the slot.
            return segment._slots[i].Item!;
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
 
                        // We determine the new segment's length based on the old length.
                        // In general, we double the size of the segment, to make it less likely
                        // that we'll need to grow again.  However, if the tail segment is marked
                        // as preserved for observation, something caused us to avoid reusing this
                        // segment, and if that happens a lot and we grow, we'll end up allocating
                        // lots of wasted space.  As such, in such situations we reset back to the
                        // initial segment length; if these observations are happening frequently,
                        // this will help to avoid wasted memory, and if they're not, we'll
                        // relatively quickly grow again to a larger size.
                        int nextSize = tail._preservedForObservation ? InitialSegmentLength : Math.Min(tail.Capacity, MaxSegmentLength);
                        var newTail = new ConcurrentQueueSegment<T>(nextSize);
 
                        // Hook up the new tail.
                        tail._nextSegment = newTail;
                        _tail = newTail;
                    }
                }
            }
        }
 
        /// <summary>
        /// Attempts to remove and return the object at the beginning of the <see
        /// cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <param name="result">
        /// When this method returns, if the operation was successful, <paramref name="result"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>
        /// true if an element was removed and returned from the beginning of the
        /// <see cref="ConcurrentQueue{T}"/> successfully; otherwise, false.
        /// </returns>
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

        internal bool TryDequeueSegment(out ConcurrentQueueSegment<T> segment)
        {
            while (true)
            {
                var head = _head;
                if (head._nextSegment == null)
                {
                    segment = default;
                    return false;
                }

                lock (_crossSegmentLock)
                {
                    if (head == _head)
                    {
                        segment = head;
                        _head = head._nextSegment;
                        return true;
                    }
                }
            }
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
 
        /// <summary>
        /// Attempts to return an object from the beginning of the <see cref="ConcurrentQueue{T}"/>
        /// without removing it.
        /// </summary>
        /// <param name="result">
        /// When this method returns, <paramref name="result"/> contains an object from
        /// the beginning of the <see cref="Concurrent.ConcurrentQueue{T}"/> or default(T)
        /// if the operation failed.
        /// </param>
        /// <returns>true if and object was returned successfully; otherwise, false.</returns>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than peeking.
        /// </remarks>
        public bool TryPeek([MaybeNullWhen(false)] out T result) => TryPeek(out result, resultUsed: true);
 
        /// <summary>Attempts to retrieve the value for the first element in the queue.</summary>
        /// <param name="result">The value of the first element, if found.</param>
        /// <param name="resultUsed">true if the result is needed; otherwise false if only the true/false outcome is needed.</param>
        /// <returns>true if an element was found; otherwise, false.</returns>
        private bool TryPeek([MaybeNullWhen(false)] out T result, bool resultUsed)
        {
            // Starting with the head segment, look through all of the segments
            // for the first one we can find that's not empty.
            ConcurrentQueueSegment<T> s = _head;
            while (true)
            {
                // Grab the next segment from this one, before we peek.
                // This is to be able to see whether the value has changed
                // during the peek operation.
                ConcurrentQueueSegment<T>? next = Volatile.Read(ref s._nextSegment);
 
                // Peek at the segment.  If we find an element, we're done.
                if (s.TryPeek(out result, resultUsed))
                {
                    return true;
                }
 
                // The current segment was empty at the moment we checked.
 
                if (next != null)
                {
                    // If prior to the peek there was already a next segment, then
                    // during the peek no additional items could have been enqueued
                    // to it and we can just move on to check the next segment.
                    Debug.Assert(next == s._nextSegment);
                    s = next;
                }
                else if (Volatile.Read(ref s._nextSegment) == null)
                {
                    // The next segment is null.  Nothing more to peek at.
                    break;
                }
 
                // The next segment was null before we peeked but non-null after.
                // That means either when we peeked the first segment had
                // already been frozen but the new segment not yet added,
                // or that the first segment was empty and between the time
                // that we peeked and then checked _nextSegment, so many items
                // were enqueued that we filled the first segment and went
                // into the next.  Since we need to peek in order, we simply
                // loop around again to peek on the same segment.  The next
                // time around on this segment we'll then either successfully
                // peek or we'll find that next was non-null before peeking,
                // and we'll traverse to that segment.
            }
 
            result = default;
            return false;
        }
 
        /// <summary>
        /// Removes all objects from the <see cref="ConcurrentQueue{T}"/>.
        /// </summary>
        public void Clear()
        {
            lock (_crossSegmentLock)
            {
                // Simply substitute a new segment for the existing head/tail,
                // as is done in the constructor.  Operations currently in flight
                // may still read from or write to an existing segment that's
                // getting dropped, meaning that in flight operations may not be
                // linear with regards to this clear operation.  To help mitigate
                // in-flight operations enqueuing onto the tail that's about to
                // be dropped, we first freeze it; that'll force enqueuers to take
                // this lock to synchronize and see the new tail.
                _tail.EnsureFrozenForEnqueues();
                _tail = _head = new ConcurrentQueueSegment<T>(InitialSegmentLength);
            }
        }
    }

    /// <summary>Padded head and tail indices, to avoid false sharing between producers and consumers.</summary>
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