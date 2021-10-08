using System.Diagnostics;

namespace DevTools.Threading
{
    public static class Time
    {
        public static readonly long ticks_to_µs = Stopwatch.Frequency / 1_000_000;
        public static readonly long ticks_to_ms = Stopwatch.Frequency / 1_000;
    }
}