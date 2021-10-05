namespace DevTools.Threading.Abstractions
{
    public interface IThreadPoolQueue
    {
        public void Enqueue(UnitOfWork unitOfWork, bool forceGlobal = false);
        
        public bool TryDequeue(out UnitOfWork unitOfWork);
    }
}