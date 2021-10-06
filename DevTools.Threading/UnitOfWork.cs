using System;
using System.Diagnostics.CodeAnalysis;

namespace DevTools.Threading
{
    /// <summary>
    /// Wrapper for unit of work for scheduling into dedicated thread pool 
    /// </summary>
    public class UnitOfWork
    {
        private object _unitState;
        private ExecutionUnit _unit;
        private Exception _exception;
        private UnitOfWorkState _state;
        internal long ScheduledAt;

        public UnitOfWork()
        {
            
        }
        
        public UnitOfWork([NotNull] ExecutionUnit unit, ThreadPoolItemPriority priority, [NotNull] object state)
        {
            _unit = unit;
            _exception = null;
            _unitState = state;
            _state = UnitOfWorkState.Waiting;
        }

        public Exception Error => _exception;
        
        public UnitOfWorkState State => _state;

        public void Run()
        {
            try
            {
                _state = UnitOfWorkState.Running;
                _unit.Invoke(_unitState);
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