using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DevTools.Threading
{
    public static class TimeConsts
    {
        public static readonly long ticks_in_µs = Stopwatch.Frequency / 1_000_000;
        public static readonly long ticks_in_ms = Stopwatch.Frequency / 1_000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp_µs() => Stopwatch.GetTimestamp() / ticks_in_µs;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ms_to_µs(long ms) => (ms * ticks_in_ms) / ticks_in_µs;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long µs_to_ms(long µs) => (µs * ticks_in_µs) / ticks_in_ms;
    }
}