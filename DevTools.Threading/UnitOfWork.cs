using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DevTools.Threading
{
    /// <summary>
    /// Wrapper for unit of work for scheduling into dedicated thread pool 
    /// </summary>
    public struct UnitOfWork
    {
        private object _unitState;
        private ExecutionUnit _unit;
        private Exception _exception;
        private UnitOfWorkState _state;
        internal long InternalObjectIndex;
        internal static long _internalObjectIndexCounter = 0;

        public UnitOfWork([NotNull] ExecutionUnit unit, ThreadPoolItemPriority priority, [NotNull] object state)
        {
            _unit = unit;
            _exception = null;
            _unitState = state;
            _state = UnitOfWorkState.Waiting;
            InternalObjectIndex = Interlocked.Increment(ref _internalObjectIndexCounter);
        }

        public Exception Error => _exception;
        
        public UnitOfWorkState State => _state;

        public void Run()
        {
            try
            {
                _state = UnitOfWorkState.Running;
                var copy = ExecutionContext.Capture();
                _unit.Invoke(_unitState);
                ExecutionContext.Restore(copy);
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