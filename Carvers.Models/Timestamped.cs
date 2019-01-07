using System;

namespace Carvers.Models
{

    public class Timestamped<T>
    {
        public Timestamped(DateTimeOffset timestamp, T val)
        {
            Timestamp = timestamp;
            Val = val;
        }

        public DateTimeOffset Timestamp { get; private set; }
        public T Val { get; private set; }
    }
}