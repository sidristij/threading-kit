using System.Threading;

namespace DevTools.Threading
{
    public interface IExecutionSegment
    {
        SegmentStatus Status { get; }
        
        ExecutionSegmentLogicBase Logic { get; }
        
        void SetExecutingUnit(SendOrPostCallback callback);
        
        void SetExecutingUnit(ExecutionSegmentLogicBase logic, SendOrPostCallback callback);
        
        ThreadState GetThreadStatus();
        
        void RequestThreadStop();
        
        WaitHandle RequestThreadStopAndGetWaitHandle();
        
    }
    
    public enum SegmentStatus
    {
        Running,
        Paused,
        Freezed,
        Stopped
    }
}