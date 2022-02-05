using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using DevTools.Threading;

namespace Demo
{
    partial class Program
    {
        static void Main()
        {
            Console.WriteLine($".NET: {Environment.Version}");
            // TestBlockedThreadsGeneric();
            // TestBlockedThreadsSmart();
            // TestMillionOfSuperShortMethods();
            // Console.WriteLine(TestDirect());
            DeepPerformanceTest(new GenericThreadPool(1, Environment.ProcessorCount*2));
        }

        static ConcurrentQueue<Record> queue = new ();
        static unsafe delegate*<long> rdtsc;
        static long[] lastValues = new long[100];
        static CountdownEvent cntdwn;

        static unsafe void DeepPerformanceTest(GenericThreadPool pool)
        {
            rdtsc = CreateRdtscMethod();
	
            var cycles = new int[]
            {
                0, 5, 11, 51, 101, 501, 1001, 5001,
                10001,
                100001
            };
	
            Console.WriteLine("| # cycles | TP, ticks | Body, Ticks | Cost, ticks | TP, %% | Body, %% |");
            for (int j = 0; j < cycles.Length; j++)
            {
                var sw = Stopwatch.StartNew();
                cntdwn = new CountdownEvent(500_000);
                
                for (int i = 0; i < 500_000; i++)
                {
                        // ThreadPool.QueueUserWorkItem(TraceWork, cycles[j]);
                        pool.Enqueue(TraceWork, cycles[j], false);
                }
                
                cntdwn.Wait();

                var lst = queue.OrderBy(x => x.PoolTicks).Skip(2000).Take(496000).ToList();
                var tp = lst.Average(x => x.PoolTicks);
                var bd = lst.Average(x => x.BodyTicks);
                var wait_ticks = sw.ElapsedMilliseconds;

                var step_price = ((tp + bd) / cycles[j]);
                var pool_percent = (100.0 / ((tp + bd)) * tp);
                var body_percent = (100.0 / ((tp + bd)) * bd);
                var parallelism =
                    pool.MaxHistoricalParallelismLevel;
                    // ThreadPool.ThreadCount;
                Console.WriteLine($"| {cycles[j]} | {(int)tp} | {(int)bd} | {step_price:F2} |" +
                                  $" {pool_percent:F2} | {body_percent:F2} |" +
                                  $" {wait_ticks} |" +
                                  $" {parallelism} |" +
                                  $" {parallelism * wait_ticks * pool_percent / 1000 :F2} |" +
                                  $" {parallelism * wait_ticks * body_percent / 1000 :F2} |"
                                  );

                queue.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        static unsafe void TraceWork(object x)
        {
            var rd_st = rdtsc();
            var count = (int)x;
            var tid = Environment.CurrentManagedThreadId;
            var last = lastValues[tid];
	
            var sum = (ulong)rd_st;
            for (var i = 0; i < count; i++)
            {
                sum += (ulong)i;
            }
            
            var body = rdtsc() - rd_st;

            if(body > 0 && (rd_st - last) > 0)
                queue.Enqueue(new Record {
                    PoolTicks = rd_st - last,
                    BodyTicks = body
                });
            cntdwn.Signal();

            lastValues[tid] = rdtsc();
        }

        struct Record {
            public long PoolTicks;
            public long BodyTicks;
        }

        static unsafe delegate*<long> CreateRdtscMethod()
        {
            var ptr = VirtualAlloc(IntPtr.Zero, (uint)rdtscAsm.Length, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            Marshal.Copy(rdtscAsm, 0, ptr, rdtscAsm.Length);
            return (delegate*<long>)ptr;
        }

        const uint PAGE_EXECUTE_READWRITE = 0x40;
        const uint MEM_COMMIT = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAlloc(
            IntPtr lpAddress, uint dwSize,
            uint flAllocationType, uint flProtect);

        static readonly byte[] rdtscAsm =
        {
            0x0F, 0x31, // rdtsc
            0xC3        // ret
        };
    }
}