namespace DevTools.Threading
{
    public class SmartThreadPoolLogic : SimplifiedLogicBase
    {
        protected override void OnStarted()
        {
            ;
        }

        protected override void OnRun(PoolWork poolWork)
        {
            poolWork.Run("Hello!");
        }

        protected override void OnStopping()
        {
            ;
        }
    }
}