namespace DevTools.Threading
{
    public enum ThreadPoolItemPriority
    {
        High = 0,
        AboveNormal = 1,
        Normal = 2,
        BelowNormal = 3,
        Low = 4,
        
        Default = Normal,
        RangeStart = High,
        RangeEnd = Low,
    }
}