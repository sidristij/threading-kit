using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable PossibleNullReferenceException

namespace DevTools.Threading
{
    /// <summary>
    /// Wrapper for unit of work for scheduling into dedicated thread pool 
    /// </summary>
    public unsafe struct PoolWork
    {
        private object _state;
        private PoolAction _action;
        
        // first parameter is just FYI: here can be argument. Real type should be built on real time. Size can be any: [1, ∞)
        private delegate*<object, void> _action_ptr;
        
        // second parameter is just FYI: here can be argument. Real type should be built on real time. Size can be any: [1, ∞)
        private delegate*<ref PoolWork, object, Task> _wrapper_ptr;

        public Task Run<TParam>(TParam param) =>
            // always runs with two parameters. so, callees should reflect this principle 
            ((delegate*<ref PoolWork, TParam, Task>)_wrapper_ptr)(ref this, param);

        public void Init(PoolAction action, object state)
        {
            _action = action;
            _state = state;
            _wrapper_ptr = &RegularMethodWrapper;
        }
        
        public void Init(delegate*<object, void> action, object state)
        {
            _action_ptr = action;
            _state = state;
            _wrapper_ptr = &RegularMethodPointerWrapper;
        }

        public void Init<T>(PoolAction<T> unit, object state)
        {
            _action = Unsafe.As<PoolAction>(unit);
            _state = state;
            _wrapper_ptr = &RegularMethodWithParameterWrapper;
        }
        
        public void Init(PoolActionAsync unit, object state)
        {
            _action = Unsafe.As<PoolAction>(unit);
            _state = state;
            _wrapper_ptr = &RegularMethodAsyncWrapper;
        }
        
        public void Init<T>(PoolActionAsync<T> unit, object state)
        {
            _action = Unsafe.As<PoolAction>(unit);
            _state = state;
            _wrapper_ptr = &RegularMethodWithParameterAsyncWrapper;
        }
        
        public void Init<TArg>(delegate*<TArg, object, void> action, object state)
        {
            _action_ptr = (delegate*<object, void>)action;
            _state = state;
            delegate*<ref PoolWork, TArg, Task> tmp = &RegularMethodPointerWithParameterWrapper<TArg>;
            _wrapper_ptr = (delegate*<ref PoolWork, object, Task>)tmp;
        }

        #region Delegating methods
        private static Task RegularMethodWrapper(ref PoolWork unit, object _)
        {
            unit._action(unit._state);
            return Task.CompletedTask;
        }

        private static Task RegularMethodPointerWrapper(ref PoolWork unit, object _)
        {
            unit._action_ptr(unit._state);
            return Task.CompletedTask;
        }

        private static Task RegularMethodWithParameterWrapper<TParam>(ref PoolWork unit, TParam parameter)
        {
            Unsafe.As<PoolAction<TParam>>(unit._action)(parameter, unit._state);
            return Task.CompletedTask;
        }

        private static Task RegularMethodPointerWithParameterWrapper<T>(ref PoolWork unit, T parameter)
        {
            ((delegate*<T, object, void>)unit._action_ptr)(parameter, unit._state);
            return Task.CompletedTask;
        }

        private static Task RegularMethodAsyncWrapper(ref PoolWork unit, object _)
        {
            var execute = unit._action;
            var state = unit._state;
            var task = new Task<Task>(() => Unsafe.As<PoolActionAsync>(execute).Invoke(state));
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            return task.Unwrap();
        }

        private static Task RegularMethodWithParameterAsyncWrapper<TParam>(ref PoolWork unit, TParam param)
        {
            var execute = unit._action;
            var state = unit._state;
            var task = new Task<Task>(() => Unsafe.As<PoolActionAsync<TParam>>(execute).Invoke(param, state));
            task.Start(TaskScheduler.FromCurrentSynchronizationContext());
            return task.Unwrap();
        }
        #endregion
    }
}