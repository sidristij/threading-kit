namespace DevTools.Threading
{
    public class SmartThreadPoolLogic : SimplifiedLogicBase
    {
        protected override void OnStarted()
        {
            ;
        }

        protected override void OnRun(UnitOfWork unitOfWork)
        {
            unitOfWork.Run("Hello!");
        }

        protected override void OnStopping()
        {
            ;
        }
    }
}