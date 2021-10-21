using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DevTools.Threading
{
    public static class TimeUtils
    {
        private static readonly long ticks_in_µs = Stopwatch.Frequency / 1_000_000;
        private static readonly long ticks_in_ms = Stopwatch.Frequency / 1_000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp_µs() => Stopwatch.GetTimestamp() / ticks_in_µs;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ms_to_µs(long ms) => ms << 10;  // 1024 ~= 1000
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long µs_to_ms(long µs) => µs >> 10;  // 1024 ~= 1000
    }
}