using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DevTools.Threading
{
    /// <summary>
    /// Wrapper for unit of work for scheduling into dedicated thread pool 
    /// </summary>
    public unsafe struct UnitOfWork
    {
        internal long InternalObjectIndex;
        private object _unitState;
        private ExecutionUnit _unit;
        private bool _hasArgument;
        internal static long _internalObjectIndexCounter = 0;
        private delegate*<ref UnitOfWork, void> run_ptr;
        private delegate*<ref UnitOfWork, object, void> run_params_ptr;

        public UnitOfWork([NotNull] ExecutionUnit unit, [NotNull] object state, bool hasArgument, bool async = false)
        {
            _unit = unit;
            _unitState = state;
            _hasArgument = hasArgument;
            InternalObjectIndex = Interlocked.Increment(ref _internalObjectIndexCounter);
            run_ptr = null;
            run_params_ptr = null;
            run_ptr = async ? &RunInternalAsync : &RunInternal;
            run_params_ptr = async ? &RunWithParamsInternalAsync : &RunWithParamsInternal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run() => run_ptr(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run<TParam>(TParam param) => run_params_ptr(ref this, param);

        private static void RunInternal(ref UnitOfWork unit)
        {
            Unsafe.As<ExecutionUnit>(unit._unit).Invoke(unit._unitState);
        }

        private static void RunWithParamsInternal<TParam>(ref UnitOfWork unit, TParam param)
        {
            Unsafe.As<ExecutionUnit<TParam>>(unit._unit).Invoke(param, unit._unitState);
        }

        private static void RunInternalAsync(ref UnitOfWork unitOfWork)
        {
            var unit = unitOfWork._unit;
            var state = unitOfWork._unitState;
            var task = new Task(() => Unsafe.As<ExecutionUnitAsync>(unit).Invoke(state));
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            task.GetAwaiter().GetResult();
        }

        private static void RunWithParamsInternalAsync<TParam>(ref UnitOfWork unitOfWork, TParam param)
        {
            var uow = unitOfWork._unit;
            var state = unitOfWork._unitState;
            var task = new Task(() => Unsafe.As<ExecutionUnitAsync<TParam>>(uow).Invoke(param, state));
            
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            task.GetAwaiter().GetResult();
        }
    }
}