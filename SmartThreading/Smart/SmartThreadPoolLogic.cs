using System.Threading.Tasks;

namespace DevTools.Threading
{
    public class SmartThreadPoolLogic : SimplifiedLogicBase
    {
        protected override void OnStarted()
        {
            ;
        }

        protected override Task OnRun(PoolWork poolWork)
        {
            return poolWork.Run("Hello!");
        }

        protected override void OnStopping()
        {
            ;
        }
    }
}