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

            var pool = new SimpleThreadPool<SimpleQueue, SimpleLogic>();
            pool.InitializedWaitHandle.WaitOne();

            TestSimplePool(pool);

            Console.ReadKey();

            TestSimplePool(pool);
            
            Console.WriteLine("done");
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

        private static void TestSimplePool(IThreadPool pool)
        {
            var @event = new CountdownEvent(10000);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 10000; i++)
            {
                pool.Enqueue(_ =>
                {
                    Thread.Sleep(1);
                    @event.Signal();
                });
            }
            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}