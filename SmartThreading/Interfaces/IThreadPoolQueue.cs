namespace DevTools.Threading
{
    internal interface IThreadPoolQueue
    {
        int GlobalCount { get; }
        
        public void Enqueue(PoolWork poolWork, bool preferLocal = false);
        
        public bool TryDequeue(ref PoolWork single);
    }
}