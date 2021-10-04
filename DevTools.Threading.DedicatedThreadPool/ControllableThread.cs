using System.Threading;
using DedicatedThreadPool.Exceptions;

namespace DedicatedThreadPool
{
    /// <summary>
    /// Encapsulates thread, which can migrate btw pools with internal logic change via changing current executing delegate.
    /// </summary>
    internal class ControllableThread
    {
        private static int _index = 0;
        private readonly Thread _thread;
        private volatile ControllableThreadStatus _status = ControllableThreadStatus.Paused;
        private volatile bool _stoppingRequested = false;
        private volatile SendOrPostCallback _nextAction;
        private readonly AutoResetEvent _event;
        private ManualResetEvent _stoppingEvent;

        public ControllableThread()
        {
            _thread = new Thread(ThreadWork);
            _thread.Name = $"{nameof(ControllableThread)} #{++_index}";
            _event = new AutoResetEvent(false);
            _thread.Start();
        }

        public ControllableThreadStatus Status => _status;

        /// <summary>
        /// Sets currently running delegate if previous one finished or plans for execution if prev one isn't finished yet 
        /// </summary>
        public void SetRunningDelegate(SendOrPostCallback callback)
        {
            if (_status == ControllableThreadStatus.Stopped)
            {
                throw new ThreadPoolException("Cannot set next action to the not running thread");
            }
            if (_nextAction != default || Interlocked.CompareExchange(ref _nextAction, callback, null) != null)
            {
                throw new ThreadPoolException("Cannot set next action to be queued into controlled thread because it supports only one next action");
            }
            _event.Set();
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
                if (_nextAction != null)
                {
                    _status = ControllableThreadStatus.Running;
                    
                    _nextAction.Invoke(default);
                    
                    _status = ControllableThreadStatus.Paused;
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

            _status = ControllableThreadStatus.Stopped;
        }
    }

    internal enum ControllableThreadStatus
    {
        Running,
        Paused,
        Stopped
    }
}