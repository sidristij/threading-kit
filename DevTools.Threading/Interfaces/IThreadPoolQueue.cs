namespace DevTools.Threading
{
    public interface IThreadPoolQueue
    {
        int GlobalCount { get; }
        
        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal = false);
        
        public UnitOfWork Dequeue(ref bool missedSteal);
    }
}