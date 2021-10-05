using System;

namespace DevTools.Threading.Exceptions
{
    public class ThreadPoolException : Exception
    {
        public ThreadPoolException(string message) : base(message)
        {
        }
        
        public ThreadPoolException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}