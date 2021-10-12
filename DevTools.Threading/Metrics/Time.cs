using System.Diagnostics;

namespace DevTools.Threading
{
    public static class Time
    {
        public static readonly long ticks_to_µs = Stopwatch.Frequency / 1_000_000;
        public static readonly int ticks_to_µs_shift = 
            ticks_to_ms switch
            {
                1 => 0,
                10 => 3,
                100 => 7,
                1_000 => 10,
                10_000 => 14,
                100_000 => 18,
                1_000_000 => 20,
                10_000_000 => 23,
                _ => 24
            };
        public static readonly long ticks_to_ms = Stopwatch.Frequency / 1_000;
        public static readonly int ticks_to_ms_shift = 
            ticks_to_ms switch
            {
                1 => 0,
                10 => 3,
                100 => 7,
                1_000 => 10,
                10_000 => 14,
                100_000 => 18,
                1_000_000 => 20,
                10_000_000 => 23,
                _ => 24
            };
    }
}