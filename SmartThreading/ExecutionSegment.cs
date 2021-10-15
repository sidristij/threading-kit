using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    /// <summary>
    /// Encapsulates thread, which can migrate btw pools with internal logic change via changing current executing delegate.
    /// Should work in pair with ThreadWrapperBase, which have work for controllable Thread
    /// </summary>
    internal class ExecutionSegment : IExecutionSegment
    {
        private readonly string _segmentName;
        private static int _counter = 1;
        private int _index = 1;
        private readonly Thread _thread;
        private volatile SegmentStatus _status = SegmentStatus.Paused;
        private volatile bool _stoppingRequested = false;
        private volatile ConcurrentQueue<QueueItem> _nextActions = new();
        private readonly AutoResetEvent _event;

        internal bool Freezed = false;

        public ExecutionSegment(string segmentName = default)
        {
            Logic = default;
            _segmentName = segmentName;
            _index = Interlocked.Increment(ref _counter);
            _thread = new Thread(ThreadWork);
            _thread.IsBackground = true;
            _thread.Name = BuildName();
            _event = new AutoResetEvent(false);
            _thread.Start();
        }

        public ExecutionSegmentLogicBase Logic { get; private set; }

        public void SetExecutingUnit(SendOrPostCallback callback)
        {
            SetExecutingUnit(default, callback);
        }

        /// <summary>
        /// Sets currently running delegate if previous one finished or plans for execution if prev one isn't finished yet 
        /// </summary>
        public void SetExecutingUnit(ExecutionSegmentLogicBase logic, SendOrPostCallback callback)
        {
            if (_status == SegmentStatus.Stopped)
            {
                throw new ThreadPoolException("Cannot set next action to the not running thread");
            }
            
            _nextActions.Enqueue(new QueueItem
            {
                Logic = logic,
                Callback = callback
            });

            // if status is still Paused, knock it 
            if (_status == SegmentStatus.Paused)
            {
                _event.Set();
            }
        }

        public ThreadState GetThreadStatus() => _thread.ThreadState;

        public void RequestThreadStop()
        {
            _stoppingRequested = true;
        }

        private void ThreadWork()
        {
            while (_stoppingRequested == false)
            {
                if (_nextActions.TryDequeue(out var queueItem))
                {
                    _status = SegmentStatus.Running;
                    Logic = queueItem.Logic;
                    queueItem.Callback.Invoke(default);
                    Logic = default;
                }
                else
                {
                    _status = SegmentStatus.Paused;
                    _event.WaitOne();
                }
            }

            _status = SegmentStatus.Stopped;
        }

        private string BuildName()
        {
            return _segmentName ?? $"{nameof(ExecutionSegment)} #{_index}";
        }

        public override int GetHashCode()
        {
            return _thread.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(obj is ExecutionSegment es)
            {
                return es._thread == _thread;
            }
            return false;
        }

        private struct QueueItem
        {
            public ExecutionSegmentLogicBase Logic;
            public SendOrPostCallback Callback;
        }
    }
}