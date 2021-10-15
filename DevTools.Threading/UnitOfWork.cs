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
        private object _unitState;
        private ExecutionUnit _execute;
        private delegate*<object, void> _action_ptr;
        private delegate*<ref UnitOfWork, object, void> wrapper_ptr;

        public void Init(ExecutionUnit unit, object state)
        {
            _execute = unit;
            _unitState = state;
            wrapper_ptr = &RegularMethodWrapper;
        }
        
        public void Init(delegate*<object, void> action, object state)
        {
            _action_ptr = action;
            _unitState = state;
            wrapper_ptr = &RegularMethodPointerWrapper;
        }

        public void Init<T>(ExecutionUnit<T> unit, object state)
        {
            _execute = Unsafe.As<ExecutionUnit>(unit);
            _unitState = state;
            wrapper_ptr = &RegularMethodWithParameterWrapper;
        }
        
        public void Init(ExecutionUnitAsync unit, object state)
        {
            _execute = Unsafe.As<ExecutionUnit>(unit);
            _unitState = state;
            wrapper_ptr = &RegularMethodAsyncWrapper;
        }
        
        public void Init<T>(ExecutionUnitAsync<T> unit, object state)
        {
            _execute = Unsafe.As<ExecutionUnit>(unit);
            _unitState = state;
            wrapper_ptr = &RegularMethodWithParameterAsyncWrapper;
        }
        
        public void Init<T>(delegate*<T, object, void> action, object state)
        {
            _action_ptr = (delegate*<object, void>)action;
            _unitState = state;
            delegate*<ref UnitOfWork, T, void> tmp = &RegularMethodPointerWithParameterWrapper<T>;
            wrapper_ptr = (delegate*<ref UnitOfWork, object, void>)tmp;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run<TParam>(TParam param) => ((delegate*<ref UnitOfWork, TParam, void>)wrapper_ptr)(ref this, param);

        private static void RegularMethodWrapper(ref UnitOfWork unit, object _) => unit._execute(unit._unitState);

        private static void RegularMethodPointerWrapper(ref UnitOfWork unit, object _) => unit._action_ptr(unit._unitState);

        private static void RegularMethodWithParameterWrapper<TParam>(ref UnitOfWork unit, TParam parameter) => 
            Unsafe.As<ExecutionUnit<TParam>>(unit._execute)(parameter, unit._unitState);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void RegularMethodPointerWithParameterWrapper<T>(ref UnitOfWork unit, T parameter) => 
            ((delegate*<T, object, void>)unit._action_ptr)(parameter, unit._unitState);

        private static void RegularMethodAsyncWrapper(ref UnitOfWork unit, object _)
        {
            var execute = unit._execute;
            var state = unit._unitState;
            var task = new Task(() => Unsafe.As<ExecutionUnitAsync>(execute).Invoke(state));
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            task.GetAwaiter().GetResult();
        }

        private static void RegularMethodWithParameterAsyncWrapper<TParam>(ref UnitOfWork unit, TParam param)
        {
            var execute = unit._execute;
            var state = unit._unitState;
            var task = new Task(() => Unsafe.As<ExecutionUnitAsync<TParam>>(execute).Invoke(param, state));
            
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            task.GetAwaiter().GetResult();
        }
    }
}