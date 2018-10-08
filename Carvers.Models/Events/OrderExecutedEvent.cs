using System;

namespace Carvers.Models.Events
{

    public class OrderExecutedEvent : DateTimeEvent<IOrder>
    {
        public OrderExecutedEvent(DateTimeOffset dateTimeOffset, IOrder anEvent) 
            : base(dateTimeOffset, anEvent)
        {
        }
    }
    

}