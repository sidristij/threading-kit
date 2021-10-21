namespace DevTools.Threading
{
    internal interface IThreadPoolQueue
    {
        int GlobalCount { get; }
        
        public void Enqueue(PoolActionUnit poolActionUnit, bool preferLocal = false);
        
        public bool TryDequeue(ref PoolActionUnit single);
    }
}