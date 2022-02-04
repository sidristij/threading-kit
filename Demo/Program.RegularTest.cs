using System;
using System.Diagnostics;
using System.Threading;
using DevTools.Threading;

namespace Demo
{
    partial class Program
    {
        private const int count = 1_000_000;

        private static void TestMillionOfSuperShortMethods()
        {
            // GenericThreadPool pool = default;
            var pool = new GenericThreadPool(1, Environment.ProcessorCount * 2);
            pool.InitializedWaitHandle.WaitOne();

            // var netPool = false;
            var ourPool = true;
            var netPool = true;
            // var ourPool = false;

            var sum_regular = 0;
            var sum_smart = 0;
            var first = true;
            var first2 = true;
            for (int i = 0; i < 20; i++)
            {
                if (netPool)
                {
                    var res = TestRegularPool();
                    if (!first)
                        sum_regular += res;
                    else
                        first = false;
                }

                if (ourPool)
                {
                    var res2 = TestSmartPool(pool);
                    if (!first2)
                        sum_smart += res2;
                    else
                        first2 = false;
                }

                Console.WriteLine();
            }

            if (sum_regular > 0)
            {
                var percent = (sum_smart * 100) / sum_regular;
                var res = percent - 100;
                var sign = res >= 0 ? "+" : "";
                Console.WriteLine($"AVG: {sign}{res} %");
            }

            // Console.WriteLine($"done with max = {pool.MaxHistoricalParallelismLevel} level of parallelism");
        }

        private static int TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var countdownEvent = new CountdownEvent(count);

            for (var i = 0; i < count; i++)
            {
                ThreadPool.QueueUserWorkItem(countdown => { countdown.Signal(); }, countdownEvent, false);
            }

            countdownEvent.Wait();
            sw.Stop();
            Console.Write(sw.ElapsedMilliseconds);
            Console.Write("\t");
            return (int)sw.ElapsedMilliseconds;
        }

        private static int TestSmartPool(IThreadPool<string> pool)
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(count);

            for (var i = 0; i < count; i++)
            {
                pool.Enqueue((x) => { ((CountdownEvent)x).Signal(); }, @event, false);
            }

            @event.Wait();
            sw.Stop();
            Console.Write(sw.ElapsedMilliseconds);
            return (int)sw.ElapsedMilliseconds;
        }

        private static int TestDirect()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(count);

            for (var i = 0; i < count; i++)
            {
                @event.Signal();
            }

            sw.Stop();
            Console.Write(sw.ElapsedMilliseconds);
            return (int)sw.ElapsedMilliseconds;
        }
    }
}