namespace DevTools.Threading
{
    public delegate void ExecutionUnit(object state = default);
    public delegate void ExecutionUnit<in TParam>(TParam param, object state = default);
}