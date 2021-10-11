using System;
using System.Diagnostics;
using System.Threading;
using DevTools.Threading;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {

            var pool = new SimpleThreadPool<ulong>();
            pool.InitializedWaitHandle.WaitOne();

            // TestRegularPool();
            // TestSimplePool(pool);

            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();
            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();
            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();
            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();
            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();
            TestRegularPool();
            TestSimplePool(pool); Console.WriteLine();

            // Console.ReadKey();
            
            // TestSimplePool(pool);
            
            Console.WriteLine("done");
        }

        private static void TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(1_000_0000);
            for (var i = 0; i < 1_000_0000; i++)
            {
                ThreadPool.QueueUserWorkItem((x) =>
                {
                    ((CountdownEvent)x).Signal();
                }, @event);
            }

            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private static void TestSimplePool(IThreadPool<ulong> pool)
        {
            var @event = new CountdownEvent(1_000_0000);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 1_000_0000; i++)
            {
                pool.Enqueue((arg, state)  =>
                {
                    ((CountdownEvent)state).Signal();
                }, @event);
            }
            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}