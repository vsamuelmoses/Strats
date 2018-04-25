using System;

namespace Carvers.Models
{

    public class Timestamped<T>
    {
        public Timestamped(DateTimeOffset dto, T val)
        {
            Timestamp = dto;
            Val = val;
        }

        public DateTimeOffset Timestamp { get; private set; }
        public T Val { get; private set; }
    }
}