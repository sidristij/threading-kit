using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DevTools.Threading
{
    internal class ThreadWrapper
    {
        private readonly string _threadName;
        private static int _counter = 1;
        private readonly int _index = 1;
        private readonly Thread _thread;
        private volatile SegmentStatus _status = SegmentStatus.Paused;
        private volatile bool _stoppingRequested = false;
        private volatile ConcurrentQueue<QueueItem> _nextActions = new();
        private readonly AutoResetEvent _event;

        internal bool Frozen = false;

        public ThreadWrapper(string threadName = default)
        {
            Logic = default;
            _threadName = threadName;
            _index = Interlocked.Increment(ref _counter);
            _thread = new Thread(ThreadWork);
            _thread.IsBackground = true;
            _thread.Name = BuildName();
            _event = new AutoResetEvent(false);
            _thread.Start();
        }

        public ExecutionSegmentLogicBase Logic { get; private set; }

        public ThreadState GetThreadStatus() => _thread.ThreadState;

        public void RequestThreadStop()
        {
            _stoppingRequested = true;
        }

        public void SetExecutingUnit(Action action) => SetExecutingUnit(default, action);

        public void SetExecutingUnit(ExecutionSegmentLogicBase logic, Action action)
        {
            if (_status == SegmentStatus.Stopped)
            {
                throw new ThreadPoolException("Cannot set next action to the not running thread");
            }
            
            _nextActions.Enqueue(new QueueItem
            {
                Logic = logic,
                Action = action
            });

            // if status is still Paused, knock it 
            if (_status == SegmentStatus.Paused)
            {
                _event.Set();
            }
        }

        private void ThreadWork()
        {
            while (_stoppingRequested == false)
            {
                if (_nextActions.TryDequeue(out var queueItem))
                {
                    _status = SegmentStatus.Running;
                    Logic = queueItem.Logic;
                    queueItem.Action();
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
            return _threadName ?? $"{nameof(ThreadWrapper)} #{_index}";
        }

        public override int GetHashCode()
        {
            return _thread.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(obj is ThreadWrapper es)
            {
                return es._thread == _thread;
            }
            return false;
        }

        private struct QueueItem
        {
            public ExecutionSegmentLogicBase Logic;
            public Action Action;
        }
    }
}