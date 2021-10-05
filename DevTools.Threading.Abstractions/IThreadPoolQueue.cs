namespace DevTools.Threading.Abstractions
{
    public interface IThreadPoolQueue
    {
        public int Volume { get; }
        
        public void Enqueue(UnitOfWork unitOfWork);
        
        public bool TryDequeue(out UnitOfWork unitOfWork);
    }
}