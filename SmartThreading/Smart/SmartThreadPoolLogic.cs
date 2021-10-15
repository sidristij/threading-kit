namespace DevTools.Threading
{
    public class SmartThreadPoolLogic : SimplifiedLogicBase
    {
        private ulong x;

        public SmartThreadPoolLogic()
        {
            x = 1;
        }
        
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