using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DevTools.Threading.Abstractions
{
    public class SimpleQueue : IThreadPoolQueue
    {
        private readonly WorkStealingQueue _queue = new();

        public int Volume => _queue.Count;

        public void Enqueue(UnitOfWork unitOfWork)
        {
            _queue.LocalPush(unitOfWork);
        }

        public bool TryDequeue(out UnitOfWork unitOfWork)
        {
            unitOfWork = Unsafe.As<UnitOfWork>(_queue.LocalPop());
            return unitOfWork != null;
        }
    }
}