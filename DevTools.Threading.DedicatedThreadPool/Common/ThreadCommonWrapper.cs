namespace DedicatedThreadPool
{
    public class ThreadCommonWrapper : ThreadWrapperBase
    {
        public ThreadCommonWrapper(AdaptableThreadPool threadPool) : base(threadPool)
        {
        }

        protected override void OnThreadStarted()
        {
            // ;
        }

        protected override void OnThreadStopping()
        {
            // ;
        }

        internal override void OnThreadGotWork()
        {
            
        }

        protected override void OnThreadPaused()
        {
        }
    }
}