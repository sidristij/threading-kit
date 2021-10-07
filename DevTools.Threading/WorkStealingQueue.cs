using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using DevTools.Threading;

namespace DevTools.Threading
{
    internal sealed class WorkStealingQueue
    {
        private const int INITIAL_SIZE = 32;
        internal volatile UnitOfWork[] m_array = new UnitOfWork[INITIAL_SIZE]; // SOS's ThreadPool command depends on this name
        private volatile int m_mask = INITIAL_SIZE - 1;
 
#if DEBUG
        // in debug builds, start at the end so we exercise the index reset logic.
        private const int START_INDEX = int.MaxValue;
#else
            private const int START_INDEX = 0;
#endif
 
        private volatile int m_headIndex = START_INDEX;
        private volatile int m_tailIndex = START_INDEX;
 
        private SpinLock m_foreignLock = new SpinLock(enableThreadOwnerTracking: false);
 
        public void LocalPush(UnitOfWork obj)
        {
            int tail = m_tailIndex;
 
            // We're going to increment the tail; if we'll overflow, then we need to reset our counts
            if (tail == int.MaxValue)
            {
                tail = LocalPush_HandleTailOverflow();
            }
 
            // When there are at least 2 elements' worth of space, we can take the fast path.
            if (tail < m_headIndex + m_mask)
            {
                m_array[tail & m_mask] = obj;
                m_tailIndex = tail + 1;
            }
            else
            {
                // We need to contend with foreign pops, so we lock.
                bool lockTaken = false;
                try
                {
                    m_foreignLock.Enter(ref lockTaken);
 
                    int head = m_headIndex;
                    int count = m_tailIndex - m_headIndex;
 
                    // If there is still space (one left), just add the element.
                    if (count >= m_mask)
                    {
                        // We're full; expand the queue by doubling its size.
                        var newArray = new UnitOfWork[m_array.Length << 1];
                        for (int i = 0; i < m_array.Length; i++)
                            newArray[i] = m_array[(i + head) & m_mask];
 
                        // Reset the field values, incl. the mask.
                        m_array = newArray;
                        m_headIndex = 0;
                        m_tailIndex = tail = count;
                        m_mask = (m_mask << 1) | 1;
                    }
 
                    m_array[tail & m_mask] = obj;
                    m_tailIndex = tail + 1;
                }
                finally
                {
                    if (lockTaken)
                        m_foreignLock.Exit(useMemoryBarrier: false);
                }
            }
        }
 
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int LocalPush_HandleTailOverflow()
        {
            bool lockTaken = false;
            try
            {
                m_foreignLock.Enter(ref lockTaken);
 
                int tail = m_tailIndex;
                if (tail == int.MaxValue)
                {
                    //
                    // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                    // This way we don't need to rearrange the items already in the queue; they'll be found
                    // correctly exactly where they are.  One subtlety here is that we need to make sure that
                    // if head is currently < tail, it remains that way.  This happens to just fall out from
                    // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all
                    // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible
                    // for the head to end up > than the tail, since you can't set any more bits than all of
                    // them.
                    //
                    m_headIndex &= m_mask;
                    m_tailIndex = tail = m_tailIndex & m_mask;
                    Debug.Assert(m_headIndex <= m_tailIndex);
                }
 
                return tail;
            }
            finally
            {
                if (lockTaken)
                    m_foreignLock.Exit(useMemoryBarrier: true);
            }
        }
 
        public bool LocalFindAndPop(UnitOfWork obj)
        {
            // Fast path: check the tail. If equal, we can skip the lock.
            if (m_array[(m_tailIndex - 1) & m_mask].InternalObjectIndex == obj.InternalObjectIndex)
            {
                UnitOfWork unused = LocalPop();
                return unused.InternalObjectIndex != 0;
            }
 
            // Else, do an O(N) search for the work item. The theory of work stealing and our
            // inlining logic is that most waits will happen on recently queued work.  And
            // since recently queued work will be close to the tail end (which is where we
            // begin our search), we will likely find it quickly.  In the worst case, we
            // will traverse the whole local queue; this is typically not going to be a
            // problem (although degenerate cases are clearly an issue) because local work
            // queues tend to be somewhat shallow in length, and because if we fail to find
            // the work item, we are about to block anyway (which is very expensive).
            for (int i = m_tailIndex - 2; i >= m_headIndex; i--)
            {
                if (m_array[i & m_mask].InternalObjectIndex == obj.InternalObjectIndex)
                {
                    // If we found the element, block out steals to avoid interference.
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);
 
                        // If we encountered a race condition, bail.
                        if (m_array[i & m_mask].InternalObjectIndex == 0)
                            return false;
 
                        // Otherwise, null out the element.
                        m_array[i & m_mask] = default;
 
                        // And then check to see if we can fix up the indexes (if we're at
                        // the edge).  If we can't, we just leave nulls in the array and they'll
                        // get filtered out eventually (but may lead to superfluous resizing).
                        if (i == m_tailIndex)
                            m_tailIndex--;
                        else if (i == m_headIndex)
                            m_headIndex++;
 
                        return true;
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: false);
                    }
                }
            }
 
            return false;
        }
 
        public UnitOfWork LocalPop() => m_headIndex < m_tailIndex ? LocalPopCore() : default;
 
        private UnitOfWork LocalPopCore()
        {
            while (true)
            {
                int tail = m_tailIndex;
                if (m_headIndex >= tail)
                {
                    return default;
                }
 
                // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                tail--;
                Interlocked.Exchange(ref m_tailIndex, tail);
 
                // If there is no interaction with a take, we can head down the fast path.
                if (m_headIndex <= tail)
                {
                    int idx = tail & m_mask;
                    var obj = m_array[idx];
 
                    // Check for nulls in the array.
                    if (obj.InternalObjectIndex == 0) continue;
 
                    m_array[idx] = default;
                    return obj;
                }
                else
                {
                    // Interaction with takes: 0 or 1 elements left.
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);
 
                        if (m_headIndex <= tail)
                        {
                            // Element still available. Take it.
                            int idx = tail & m_mask;
                            var obj = m_array[idx];
 
                            // Check for nulls in the array.
                            if (obj.InternalObjectIndex == 0) continue;
 
                            m_array[idx] = default;
                            return obj;
                        }
                        else
                        {
                            // If we encountered a race condition and element was stolen, restore the tail.
                            m_tailIndex = tail + 1;
                            return default;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: false);
                    }
                }
            }
        }
 
        public bool CanSteal => m_headIndex < m_tailIndex;
 
        public UnitOfWork TrySteal(ref bool missedSteal)
        {
            while (true)
            {
                if (CanSteal)
                {
                    bool taken = false;
                    try
                    {
                        m_foreignLock.TryEnter(ref taken);
                        if (taken)
                        {
                            // Increment head, and ensure read of tail doesn't move before it (fence).
                            int head = m_headIndex;
                            Interlocked.Exchange(ref m_headIndex, head + 1);
 
                            if (head < m_tailIndex)
                            {
                                int idx = head & m_mask;
                                var obj = m_array[idx];
 
                                // Check for nulls in the array.
                                if (obj.InternalObjectIndex == 0) continue;
 
                                m_array[idx] = default;
                                return obj;
                            }
                            else
                            {
                                // Failed, restore head.
                                m_headIndex = head;
                            }
                        }
                    }
                    finally
                    {
                        if (taken)
                            m_foreignLock.Exit(useMemoryBarrier: false);
                    }
 
                    missedSteal = true;
                }
 
                return default;
            }
        }
 
        public int Count
        {
            get
            {
                bool lockTaken = false;
                try
                {
                    m_foreignLock.Enter(ref lockTaken);
                    return Math.Max(0, m_tailIndex - m_headIndex);
                }
                finally
                {
                    if (lockTaken)
                    {
                        m_foreignLock.Exit(useMemoryBarrier: false);
                    }
                }
            }
        }
    }
}