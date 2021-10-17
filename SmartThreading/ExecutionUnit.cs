using System.Threading.Tasks;

namespace DevTools.Threading
{
    public delegate void ExecutionUnit(object state = default);
    public delegate void ExecutionUnitWithState<TState>(TState state = default);
    
    public delegate Task ExecutionUnitAsync(object state = default);
    public delegate Task ExecutionUnitAsyncWithState<TState>(TState state = default);
    
    public delegate void ExecutionUnit<TParam>(TParam param, object state = default);
    public delegate void ExecutionUnitWithState<TParam, TState>(TParam param, TState state = default);
    
    public delegate Task ExecutionUnitAsync<TParam>(TParam param, object state = default);
    public delegate Task ExecutionUnitAsyncWithState<TParam, TState>(TParam param, TState state = default);
}