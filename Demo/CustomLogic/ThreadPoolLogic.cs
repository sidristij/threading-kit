using System.Threading.Tasks;
using DevTools.Threading;

namespace DevTools.Threading
{
    public class ThreadPoolLogic : ExecutionSegmentLogicBase
    {
        protected override void OnStarted()
        {
            ;
        }

        protected override Task OnRun(PoolActionUnit poolActionUnit)
        {
            return poolActionUnit.Run("Hello!");
        }

        protected override void OnStopping()
        {
            ;
        }
    }
}