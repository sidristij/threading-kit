using System.Threading.Tasks;

namespace DevTools.Threading
{
    public delegate void PoolAction(object state = default);
    
    public delegate Task PoolActionAsync(object state = default);
    
    public delegate void PoolAction<TParam>(TParam param, object state = default);
    
    public delegate Task PoolActionAsync<TParam>(TParam param, object state = default);
}