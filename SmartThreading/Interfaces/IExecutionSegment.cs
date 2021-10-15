using System.Threading;

namespace DevTools.Threading
{
    public interface IExecutionSegment
    {
        ExecutionSegmentLogicBase Logic { get; }
        
        void SetExecutingUnit(SendOrPostCallback callback);
        
        void SetExecutingUnit(ExecutionSegmentLogicBase logic, SendOrPostCallback callback);
        
        ThreadState GetThreadStatus();
        
        void RequestThreadStop();
    }
    
    public enum SegmentStatus
    {
        Running,
        Paused,
        Stopped
    }
}