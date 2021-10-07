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
        internal long InternalObjectIndex;
        private object _unitState;
        private ExecutionUnit _unit;
        internal static long _internalObjectIndexCounter = 0;

        public UnitOfWork([NotNull] ExecutionUnit unit, [NotNull] object state)
        {
            _unit = unit;
            _unitState = state;
            InternalObjectIndex = Interlocked.Increment(ref _internalObjectIndexCounter);
        }

        public void Run()
        {
            try
            {
                var copy = ExecutionContext.Capture();
                _unit.Invoke(_unitState);
                ExecutionContext.Restore(copy);
            }
            catch (Exception _)
            {
                ;
            }
        }
    }
}