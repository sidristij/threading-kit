using System;
using System.Diagnostics;
using System.Threading;
using DevTools.Threading;
using DevTools.Threading;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            TestRegularPool();
            
            TestSimplePool();
        }

        private static void TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(100000);
            for (var i = 0; i < 100000; i++)
            {
                ThreadPool.QueueUserWorkItem((x) => { @event.Signal(); });
            }

            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private static void TestSimplePool()
        {
            var @event = new CountdownEvent(100000);
            var pool = new SimpleThreadPool<SimpleQueue, SimpleLogic>();
            pool.InitializedWaitHandle.WaitOne();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                pool.Enqueue(_ => { @event.Signal(); });
            }
            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}