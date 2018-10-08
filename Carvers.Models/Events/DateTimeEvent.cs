using System;

namespace Carvers.Models.Events
{

    public class DateTimeEvent<T> : IEvent
    {
        public DateTimeEvent(DateTimeOffset dateTimeOffset, T anEvent)
        {
            DateTimeOffset = dateTimeOffset;
            Event = anEvent;
        }

        public T Event { get; }
        public DateTimeOffset DateTimeOffset { get; }
    }
    

}