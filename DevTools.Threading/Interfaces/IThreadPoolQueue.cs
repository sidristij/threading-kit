namespace DevTools.Threading
{
    internal interface IThreadPoolQueue
    {
        int GlobalCount { get; }
        
        public void Enqueue(UnitOfWork unitOfWork, bool preferLocal = false);
        
        public void Dequeue(ref UnitOfWork single);
    }
}