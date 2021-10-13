using System.Threading.Tasks;

namespace DevTools.Threading
{
    public delegate void ExecutionUnit(object state = default);
    public delegate void ExecutionUnit<TParam>(TParam param, object state = default);
}