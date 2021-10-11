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
            var pool = new SmartThreadPool<ulong>();
            pool.InitializedWaitHandle.WaitOne();

            // TestRegularPool();
            // TestSimplePool(pool);

            var netPool = false;

            if (netPool) TestRegularPool();
            TestSimplePool(pool);
            if (netPool) TestRegularPool();
            TestSimplePool(pool);
            if (netPool) TestRegularPool();
            TestSimplePool(pool);
            if (netPool) TestRegularPool();
            TestSimplePool(pool);
            if (netPool) TestRegularPool();
            TestSimplePool(pool);
            if (netPool) TestRegularPool();
            TestSimplePool(pool);

            // Console.ReadKey();

            // TestSimplePool(pool);

            Console.WriteLine("done");
        }

        private static void TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(10_000_000);
            for (var i = 0; i < 10_000_000; i++)
            {
                ThreadPool.QueueUserWorkItem((x) => { ((CountdownEvent)x).Signal(); }, @event);
            }

            @event.Wait();
            Console.Write(sw.ElapsedMilliseconds);
            Console.Write("  ");
        }

        private static void TestSimplePool(IThreadPool<ulong> pool)
        {
            var @event = new CountdownEvent(10_000_000);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 10_000_000; i++)
            {
                pool.Enqueue((arg, state) => { ((CountdownEvent)state).Signal(); }, @event);
            }

            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}