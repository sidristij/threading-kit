using System;
using System.Threading;
using System.Threading.Tasks;
using DevTools.Threading;

namespace Demo
{
    partial class Program
    {
        static void TestBlockedThreadsGeneric()
        {
            ThreadPool.GetMaxThreads(out _, out var completionPortThreads);
            Console.WriteLine(ThreadPool.SetMaxThreads(16, completionPortThreads));
            
            var countdownEvent = new CountdownEvent(16);
            var badBlockingEvent = new ManualResetEvent(false);

            for (int i = 0; i < 16; i++)
            {
                ThreadPool.QueueUserWorkItem(async (state) =>
                {
                    badBlockingEvent.WaitOne();
                    await MethodAsync();
                    ((CountdownEvent)state).Signal();
                }, countdownEvent, true);
            }

            ;
            ThreadPool.QueueUserWorkItem(async (state) =>
            {
                badBlockingEvent.Set();
            }, countdownEvent, false);

            countdownEvent.Wait();
        }
        
        static void TestBlockedThreadsSmart()
        {
            var pool = new SmartThreadPool<ThreadLifetimeLogic, ParallelismStrategy, ulong>(minAllowedThreads: 16, maxAllowedWorkingThreads: 16);
            pool.InitializedWaitHandle.WaitOne();
            
            var countdownEvent = new CountdownEvent(16);
            var badBlockingEvent = new ManualResetEvent(false);

            for (int i = 0; i < 16; i++)
            {
                pool.Enqueue(async (p, state) =>
                {
                    badBlockingEvent.WaitOne();
                    await MethodAsync();
                    ((CountdownEvent)state).Signal();
                }, countdownEvent, false);
            }

            pool.Enqueue(async (p, state) =>
            {
                badBlockingEvent.Set();
            }, countdownEvent, false);

            countdownEvent.Wait();
        }

        async static Task MethodAsync()
        {
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
        }
    }
}

