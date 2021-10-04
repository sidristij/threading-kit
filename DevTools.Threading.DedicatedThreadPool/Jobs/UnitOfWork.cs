using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DedicatedThreadPool.Jobs
{
    /// <summary>
    /// Wrapper for unit of work for scheduling into dedicated thread pool 
    /// </summary>
    internal struct UnitOfWork
    {
        private object _ostate;
        private SendOrPostCallback _action;
        private Exception _exception;
        private UnitOfWorkState _state;

        public UnitOfWork([NotNull] SendOrPostCallback action, [NotNull] object state)
        {
            _action = action;
            _exception = null;
            _ostate = state;
            _state = UnitOfWorkState.Waiting;
        }

        public Exception Error => _exception;
        public UnitOfWorkState State => _state;  

        public void Run()
        {
            try
            {
                _state = UnitOfWorkState.Running;
                _action.Invoke(_ostate);
                _state = UnitOfWorkState.Finished;
            }
            catch (Exception exception)
            {
                _exception = exception;
                _state = UnitOfWorkState.Failed;
            }
        }
    }
}