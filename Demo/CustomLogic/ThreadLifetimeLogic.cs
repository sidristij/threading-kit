using System.Threading.Tasks;

namespace DevTools.Threading
{
    public class ThreadLifetimeLogic : ExecutionSegmentLogicBase
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