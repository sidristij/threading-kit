namespace DevTools.Threading
{
    public class SimpleLogic : SimplifiedLogicBase
    {
        private ulong x;

        public SimpleLogic()
        {
            x = 1;
        }
        
        protected override void OnStarted()
        {
            ;
        }

        protected override void OnRun(UnitOfWork unitOfWork)
        {
            unitOfWork.Run(x);
        }

        protected override void OnStopping()
        {
            ;
        }
    }
}