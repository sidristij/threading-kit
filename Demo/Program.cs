﻿using System;
using System.Diagnostics;
using System.Threading;
using DevTools.Threading;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {

            var pool = new SimpleThreadPool<SimpleQueue, SimpleLogic, int>();
            pool.InitializedWaitHandle.WaitOne();

            TestRegularPool();
            TestSimplePool(pool);

            TestRegularPool();
            TestSimplePool(pool);
            
            TestRegularPool();
            TestSimplePool(pool);

            // Console.ReadKey();
            
            // TestSimplePool(pool);
            
            Console.WriteLine("done");
        }

        private static void TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(1000000);
            for (var i = 0; i < 1000000; i++)
            {
                ThreadPool.QueueUserWorkItem((x) => { @event.Signal(); });
            }

            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private static void TestSimplePool(IThreadPool<int> pool)
        {
            var @event = new CountdownEvent(1_000_000);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 1_000_000; i++)
            {
                pool.Enqueue((parameter, state)  =>
                {
                    @event.Signal();
                });
            }
            @event.Wait();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}