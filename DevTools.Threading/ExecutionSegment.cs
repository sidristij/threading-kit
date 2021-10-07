using System.Collections.Concurrent;
using System.Threading;
using DevTools.Threading;
using DevTools.Threading.Exceptions;

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
        private volatile ConcurrentQueue<SendOrPostCallback> _nextActions = new();
        private readonly AutoResetEvent _event;
        private ManualResetEvent _stoppingEvent;

        public ExecutionSegment(string segmentName = default)
        {
            _segmentName = segmentName;
            _index = Interlocked.Increment(ref _counter);
            _thread = new Thread(ThreadWork);
            _thread.IsBackground = true;
            _thread.Name = BuildName();
            _event = new AutoResetEvent(false);
            _thread.Start();
        }

        public SegmentStatus Status => _status;

        /// <summary>
        /// Sets currently running delegate if previous one finished or plans for execution if prev one isn't finished yet 
        /// </summary>
        public void SetExecutingUnit(SendOrPostCallback callback)
        {
            if (_status == SegmentStatus.Stopped)
            {
                throw new ThreadPoolException("Cannot set next action to the not running thread");
            }
            
            _nextActions.Enqueue(callback);

            // if status is still Paused, knock it 
            if (_status == SegmentStatus.Paused)
            {
                _event.Set();
            }
        }

        public void RequestThreadStop()
        {
            _stoppingRequested = true;
        }
        
        /// <summary>
        /// Requests thread stop and returns awaitable handle
        /// </summary>
        public WaitHandle RequestThreadStopAndGetWaitHandle()
        {
            _stoppingEvent = new ManualResetEvent(false);
            _stoppingRequested = true;
            _event.Set();
            return _stoppingEvent;
        }

        private void ThreadWork()
        {
            while (_stoppingRequested == false)
            {
                if (_nextActions.TryDequeue(out var callback))
                {
                    _status = SegmentStatus.Running;
                    callback.Invoke(default);
                    _status = SegmentStatus.Paused;
                }
                else
                {
                    _event.WaitOne();
                }
            }

            // Notify awaiter if have
            if (_stoppingEvent != null)
            {
                _stoppingEvent.Set();
            }

            _status = SegmentStatus.Stopped;
        }

        private string BuildName()
        {
            return _segmentName ?? $"{nameof(ExecutionSegment)} #{_index}";
        }
    }
}