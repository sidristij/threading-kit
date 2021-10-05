﻿using System.Threading;

namespace DevTools.Threading
{
    public interface IExecutionSegment
    {
        SegmentStatus Status { get; }
        
        void SetExecutingUnit(SendOrPostCallback callback);
        
        void RequestThreadStop();
        
        WaitHandle RequestThreadStopAndGetWaitHandle();
    }
    
    public enum SegmentStatus
    {
        Running,
        Paused,
        Stopped
    }
}