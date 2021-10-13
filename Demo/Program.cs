using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DevTools.Threading;

namespace Demo
{
    class Program
    {
        private const int count = 1_000_000;

        static void Main1()
        {
            var pool = new SmartThreadPool<ulong>( 4, Environment.ProcessorCount);
            var @event = new CountdownEvent(1);
            pool.Enqueue(async (p, state) =>
            {
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                await MethodAsync();
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                ((CountdownEvent)state).Signal();
            }, @event, false);

            @event.Wait();
        }

        async static Task MethodAsync()
        {
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
        }
        
        static void Main(string[] args)
        {
            var pool = new SmartThreadPool<object>( 1, Environment.ProcessorCount);

            pool.InitializedWaitHandle.WaitOne();

            // var netPool = false;
            // var ourPool = true;
            var netPool = true;
            var ourPool = false;
            
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
                    var res2 = TestSimplePool(pool);
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
            
            Console.WriteLine($"done with max = {pool.MaxThreadsGot} level of parallelism");
        }

        private static int TestRegularPool()
        {
            var sw = Stopwatch.StartNew();
            var @event = new CountdownEvent(count);
            for (var i = 0; i < count; i++)
            {
                ThreadPool.QueueUserWorkItem((x) => { ((CountdownEvent)x).Signal(); }, @event, false);
            }

            @event.Wait();
            sw.Stop();
            Console.Write(sw.ElapsedMilliseconds);
            Console.Write("\t");
            return (int)sw.ElapsedMilliseconds;
        }

        private static int TestSimplePool(IThreadPool<object> pool)
        {
            var @event = new CountdownEvent(count);
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
            {
                pool.Enqueue((p, state) =>
                {
                    ((CountdownEvent)state).Signal();
                }, @event, false);
            }

            @event.Wait();
            sw.Stop();
            Console.Write(sw.ElapsedMilliseconds);
            return (int)sw.ElapsedMilliseconds;
        }
    }
}