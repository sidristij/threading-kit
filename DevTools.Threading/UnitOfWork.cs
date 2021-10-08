using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
                _unit?.Invoke(_unitState);
            }
            catch (Exception _)
            {
                ;
            }
        }

        public void Run<TParam>(TParam param)
        {
            try
            {
                Unsafe.As<ExecutionUnit<TParam>>(_unit)?.Invoke(param, _unitState);
            }
            catch (Exception _)
            {
                ;
            }
        }
    }
}